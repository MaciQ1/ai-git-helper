using System.Threading.RateLimiting;
using CommitGenerator.Configuration;
using CommitGenerator.Documentation;
using CommitGenerator.Models;
using CommitGenerator.Security;
using CommitGenerator.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

const int MaxGitDiffLength = 200_000;

builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = MaxGitDiffLength + 10_000);

builder.Services
    .AddOptions<AiOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        options.ApiKey = configuration["OPENAI_API_KEY"] ?? string.Empty;
        options.BaseUrl = configuration["OPENAI_BASE_URL"] ?? AiOptions.DefaultBaseUrl;
        options.Model = configuration["OPENAI_MODEL"] ?? AiOptions.DefaultModel;
    })
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey),
        "OPENAI_API_KEY musi być ustawiony.")
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp),
        "OPENAI_BASE_URL musi być poprawnym adresem HTTP(S).")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Model),
        "OPENAI_MODEL musi być ustawiony.")
    .ValidateOnStart();

builder.Services
    .AddOptions<SecurityOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        options.ApiKey = configuration["APP_API_KEY"] ?? string.Empty;
        options.ApiKeys = configuration["APP_API_KEYS"] ?? string.Empty;
    })
    .Validate(options =>
    {
        try
        {
            var keys = options.GetConfiguredKeys();
            return keys.Count > 0
                && keys.All(key => key.Name.Length > 0
                    && key.Key.Length >= SecurityOptions.MinimumApiKeyLength);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }, $"APP_API_KEY lub APP_API_KEYS musi zawierać klucz o długości co najmniej {SecurityOptions.MinimumApiKeyLength} znaków.")
    .ValidateOnStart();

builder.Services.AddHttpClient<AiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

var allowedOrigins = (builder.Configuration["FRONTEND_ORIGINS"]
        ?? "http://localhost:3000,http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins)
        .WithHeaders("Content-Type", SecurityOptions.ApiKeyHeaderName)
        .WithMethods("GET", "POST", "OPTIONS")));

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI Git Helper API",
        Version = "v1",
        Description = "Generuje commit message i opis Pull Request na podstawie git diff."
    });
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = SecurityOptions.ApiKeyHeaderName,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Klucz skonfigurowany przez APP_API_KEY lub APP_API_KEYS."
    });
    options.OperationFilter<ApiKeyOperationFilter>();
});
builder.Services.AddSingleton<ApiKeyValidator>();
builder.Services.AddSingleton<ISecretRedactor, SecretRedactor>();
builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://www.rfc-editor.org/rfc/rfc6585#section-4",
            title = "Too Many Requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Przekroczono limit 5 żądań na minutę dla tego klucza."
        }, cancellationToken);
    };
    options.AddPolicy("commit-generation", context =>
    {
        var validator = context.RequestServices.GetRequiredService<ApiKeyValidator>();
        var providedKey = context.Request.Headers[SecurityOptions.ApiKeyHeaderName];
        var partitionKey = providedKey.Count == 1
            && validator.TryGetUserName(providedKey[0]!, out var userName)
                ? $"user:{userName}"
                : "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("ENABLE_SWAGGER"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();

app.MapPost("/api/generate-commit", async (
    GenerateCommitRequest? request,
    AiClient aiClient,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request?.GitDiff))
        return Results.BadRequest(new { error = "Pole gitDiff nie może być puste." });

    if (request.GitDiff.Length > MaxGitDiffLength)
        return Results.BadRequest(new
        {
            error = $"Git diff jest zbyt duży (maksymalnie {MaxGitDiffLength:N0} znaków)."
        });

    try
    {
        var result = await aiClient.GenerateAsync(request.GitDiff, cancellationToken);
        return Results.Ok(result);
    }
    catch (AiClientException exception)
    {
        logger.LogWarning(exception, "Generowanie commita przez usługę AI nie powiodło się.");
        return Results.Problem(
            "Usługa AI nie zwróciła poprawnego wyniku.",
            statusCode: StatusCodes.Status502BadGateway);
    }
})
    .RequireAuthorization()
    .RequireRateLimiting("commit-generation")
    .WithName("GenerateCommit")
    .Produces<GenerateCommitResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .ProducesProblem(StatusCodes.Status502BadGateway)
    .WithOpenApi(operation =>
    {
        operation.Summary = "Generuje commit message i opis Pull Request";
        operation.Description = "Analizuje git diff po uprzedniej redakcji typowych sekretów.";
        return operation;
    });

app.Run();

public partial class Program { }
