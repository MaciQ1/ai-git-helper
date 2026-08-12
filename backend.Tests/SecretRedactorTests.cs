using CommitGenerator.Services;
using Xunit;

namespace CommitGenerator.Tests;

public sealed class SecretRedactorTests
{
    private readonly SecretRedactor _redactor = new();

    [Fact]
    public void Redact_RemovesNamedSecretsAndBearerTokens()
    {
        const string diff = "OPENAI_API_KEY=sk-123456789012345678901234\nAuthorization: Bearer abc.def.ghi";

        var redacted = _redactor.Redact(diff);

        Assert.DoesNotContain("sk-123456789012345678901234", redacted);
        Assert.DoesNotContain("Bearer abc.def.ghi", redacted);
        Assert.Contains("OPENAI_API_KEY=[REDACTED]", redacted);
        Assert.Contains("Bearer [REDACTED]", redacted);
    }

    [Fact]
    public void Redact_RemovesPrivateKeyBlocks()
    {
        const string diff = "-----BEGIN PRIVATE KEY-----\nsecret-content\n-----END PRIVATE KEY-----";

        var redacted = _redactor.Redact(diff);

        Assert.DoesNotContain("secret-content", redacted);
        Assert.DoesNotContain("BEGIN PRIVATE KEY", redacted);
        Assert.Equal("[REDACTED]", redacted);
    }
}
