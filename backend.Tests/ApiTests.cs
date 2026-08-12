using System.Net;
using System.Net.Http.Json;
using CommitGenerator.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CommitGenerator.Tests;

public sealed class ApiTests : IClassFixture<ApiFactory>
{
    private const string TestApiKey = "test-api-key-123456789012345678901234";
    private readonly HttpClient _client;

    public ApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", TestApiKey);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"status\":\"ok\"}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GenerateCommit_RejectsEmptyDiff()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/generate-commit",
            new GenerateCommitRequest(string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateCommit_RejectsMissingApiKey()
    {
        using var unauthenticatedClient = new ApiFactory().CreateClient();

        var response = await unauthenticatedClient.PostAsJsonAsync(
            "/api/generate-commit",
            new GenerateCommitRequest("diff"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_DescribesProtectedGenerationEndpoint()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/generate-commit", document);
        Assert.Contains("X-API-Key", document);
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENAI_API_KEY"] = "test-key",
                ["OPENAI_BASE_URL"] = "https://example.test/v1/chat/completions",
                ["OPENAI_MODEL"] = "test-model",
                ["APP_API_KEY"] = "test-api-key-123456789012345678901234"
            });
        });
    }
}
