namespace BusinessLogic.Options;

public class AiModelOptions
{
    /// <summary>OpenAI | Ollama</summary>
    public string Provider { get; set; } = "Ollama";

    public bool Enabled { get; set; } = true;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "llama3.2";

    public string BaseUrl { get; set; } = "http://localhost:11434/v1/";

    public double Temperature { get; set; } = 0.3;

    public int MaxTokens { get; set; } = 1024;

    /// <summary>Giới hạn token đầu ra riêng cho tóm tắt — thấp hơn = nhanh hơn.</summary>
    public int SummaryMaxTokens { get; set; } = 512;

    public bool IsRemoteAiConfigured()
    {
        if (string.Equals(Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
            return true;

        var key = ApiKey?.Trim() ?? "";
        return key.Length > 0 && !key.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
    }

    public bool RequiresBearerToken() =>
        !string.Equals(Provider, "Ollama", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}
