namespace CommitGenerator.Configuration;

public sealed class AiOptions
{
    public const string DefaultBaseUrl = "https://api.openai.com/v1/chat/completions";
    public const string DefaultModel = "gpt-4o-mini";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = DefaultBaseUrl;
    public string Model { get; set; } = DefaultModel;
}
