using System.Text.RegularExpressions;

namespace CommitGenerator.Services;

public sealed partial class SecretRedactor : ISecretRedactor
{
    private const string RedactedValue = "[REDACTED]";

    public string Redact(string value)
    {
        var redacted = PemBlockRegex().Replace(value, RedactedValue);
        redacted = BearerTokenRegex().Replace(redacted, $"Bearer {RedactedValue}");
        redacted = NamedSecretRegex().Replace(redacted, match =>
            match.Groups["prefix"].Value + RedactedValue);
        redacted = KnownTokenRegex().Replace(redacted, RedactedValue);

        return redacted;
    }

    [GeneratedRegex(
        "-----BEGIN [^-]+-----[\\s\\S]*?-----END [^-]+-----",
        RegexOptions.IgnoreCase)]
    private static partial Regex PemBlockRegex();

    [GeneratedRegex("\\bBearer\\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(
        "(?<prefix>\\b(?:[A-Z0-9]+_)*(?:api[-_]?key|access[-_]?token|auth[-_]?token|token|secret|password|passwd|private[-_]?key)\\b\\s*[:=]\\s*)(?:\"[^\"\\r\\n]*\"|'[^'\\r\\n]*'|[^\\s,;]+)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex NamedSecretRegex();

    [GeneratedRegex(
        "\\b(?:sk-[A-Za-z0-9]{20,}|gh[pousr]_[A-Za-z0-9_]{20,}|AIza[0-9A-Za-z_-]{20,}|(?:AKIA|ASIA)[A-Z0-9]{16})\\b")]
    private static partial Regex KnownTokenRegex();
}
