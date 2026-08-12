namespace CommitGenerator.Configuration;

public sealed class SecurityOptions
{
    public const string ApiKeyHeaderName = "X-API-Key";
    public const int MinimumApiKeyLength = 32;

    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeys { get; set; } = string.Empty;

    public IReadOnlyList<ApiKeyDefinition> GetConfiguredKeys()
    {
        var keys = new List<ApiKeyDefinition>();

        if (!string.IsNullOrWhiteSpace(ApiKey))
            keys.Add(new ApiKeyDefinition("default", ApiKey.Trim()));

        if (!string.IsNullOrWhiteSpace(ApiKeys))
        {
            foreach (var item in ApiKeys.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = item.IndexOf(':');
                if (separator <= 0 || separator == item.Length - 1)
                    throw new InvalidOperationException(
                        "APP_API_KEYS musi mieć format name:key;name2:key2.");

                var name = item[..separator].Trim();
                var key = item[(separator + 1)..].Trim();
                keys.Add(new ApiKeyDefinition(name, key));
            }
        }

        return keys;
    }
}

public sealed record ApiKeyDefinition(string Name, string Key);
