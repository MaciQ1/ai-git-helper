using System.Net;
using System.Text.Json;
using CommitGenerator.Configuration;
using CommitGenerator.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CommitGenerator.Tests;

public sealed class AiClientTests
{
    [Fact]
    public async Task GenerateAsync_MapsProviderResponseToApplicationResponse()
    {
        using var handler = new StubHttpMessageHandler((request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://example.test/v1/chat/completions", request.RequestUri?.ToString());
            Assert.Equal("Bearer test-key", request.Headers.Authorization?.ToString());

            return Task.FromResult(CreateProviderResponse(
                "{\"commitMessage\":\"feat: add generator\",\"pullRequestDescription\":\"### Summary\\n- Added generator\\n\\n### Testing\\n- dotnet test\"}"));
        });
        var client = CreateClient(handler);

        var result = await client.GenerateAsync("diff --git a/file b/file", CancellationToken.None);

        Assert.Equal("feat: add generator", result.CommitMessage);
        Assert.Contains("### Testing", result.PullRequestDescription);
    }

    [Fact]
    public async Task GenerateAsync_AcceptsJsonWrappedInMarkdownFence()
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(CreateProviderResponse(
            "```json\n{\"commitMessage\":\"fix: handle empty diff\",\"pullRequestDescription\":\"### Summary\"}\n```")));
        var client = CreateClient(handler);

        var result = await client.GenerateAsync("diff", CancellationToken.None);

        Assert.Equal("fix: handle empty diff", result.CommitMessage);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsAiClientExceptionForProviderError()
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            CreateResponse("{\"error\":\"rate limited\"}", HttpStatusCode.TooManyRequests)));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<AiClientException>(() =>
            client.GenerateAsync("diff", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_ThrowsAiClientExceptionForMalformedProviderResponse()
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            CreateResponse("{\"choices\":[{\"message\":{\"content\":\"not-json\"}}]}")));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<AiClientException>(() =>
            client.GenerateAsync("diff", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_RedactsSecretsBeforeSendingPrompt()
    {
        using var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            var body = await request.Content!.ReadAsStringAsync();

            Assert.Contains("[REDACTED]", body);
            Assert.DoesNotContain("sk-123456789012345678901234", body);
            return CreateProviderResponse(
                "{\"commitMessage\":\"chore: redact secrets\",\"pullRequestDescription\":\"### Summary\"}");
        });
        var client = CreateClient(handler);

        await client.GenerateAsync(
            "OPENAI_API_KEY=sk-123456789012345678901234",
            CancellationToken.None);
    }

    private static AiClient CreateClient(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new AiOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1/chat/completions",
            Model = "test-model"
        }),
        NullLogger<AiClient>.Instance,
        new SecretRedactor());

    private static HttpResponseMessage CreateResponse(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK) => new(statusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage CreateProviderResponse(string generatedContent) =>
        CreateResponse(JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = generatedContent } }
            }
        }));

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request, cancellationToken);
    }
}
