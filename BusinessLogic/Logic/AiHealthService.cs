using System.Text.Json;
using Microsoft.Extensions.Options;
using BusinessLogic.DTOs;
using BusinessLogic.IBusinessLogic;
using BusinessLogic.Options;

namespace BusinessLogic.Logic;

public class AiHealthService : IAiHealthService
{
    private readonly HttpClient _http;
    private readonly AiModelOptions _ai;

    public AiHealthService(HttpClient http, IOptions<AiModelOptions> ai)
    {
        _http = http;
        _ai = ai.Value;
    }

    public async Task<AiStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var provider = _ai.Provider?.Trim() ?? "Local";
        var model = _ai.Model?.Trim() ?? "";

        if (!_ai.Enabled)
        {
            return new AiStatusDto
            {
                ConfiguredForAi = false,
                IsOnline = false,
                Provider = "Local",
                Model = model,
                Message = "AI tắt (Chat:Ai:Enabled = false). Đang dùng trả lời local."
            };
        }

        if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
            return await CheckOllamaAsync(model, cancellationToken);

        if (_ai.IsRemoteAiConfigured())
        {
            return new AiStatusDto
            {
                ConfiguredForAi = true,
                IsOnline = true,
                Provider = provider,
                Model = model,
                Message = $"Đã cấu hình {provider} (model: {model}). Key có trong config."
            };
        }

        return new AiStatusDto
        {
            ConfiguredForAi = false,
            IsOnline = false,
            Provider = "Local",
            Model = model,
            Message = "Chưa cấu hình API key. Đang dùng trả lời local."
        };
    }

    private async Task<AiStatusDto> CheckOllamaAsync(string model, CancellationToken cancellationToken)
    {
        var host = ResolveOllamaHost(_ai.BaseUrl);
        try
        {
            using var response = await _http.GetAsync($"{host}/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return OfflineOllama(model,
                    $"Ollama không phản hồi ({(int)response.StatusCode}). Mở app Ollama hoặc chạy: ollama serve");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var models = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var modelsEl))
            {
                foreach (var item in modelsEl.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameEl))
                    {
                        var name = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            models.Add(name);
                    }
                }
            }

            var hasModel = models.Any(m =>
                string.Equals(m, model, StringComparison.OrdinalIgnoreCase) ||
                m.StartsWith(model + ":", StringComparison.OrdinalIgnoreCase));

            if (models.Count == 0)
            {
                return new AiStatusDto
                {
                    ConfiguredForAi = true,
                    IsOnline = true,
                    Provider = "Ollama",
                    Model = model,
                    Message = $"Ollama đang chạy nhưng chưa có model. Chạy: ollama pull {model}"
                };
            }

            if (!hasModel)
            {
                return new AiStatusDto
                {
                    ConfiguredForAi = true,
                    IsOnline = true,
                    Provider = "Ollama",
                    Model = model,
                    Message = $"Ollama OK. Model '{model}' chưa có — chạy: ollama pull {model}"
                };
            }

            return new AiStatusDto
            {
                ConfiguredForAi = true,
                IsOnline = true,
                Provider = "Ollama",
                Model = model,
                Message = $"Ollama sẵn sàng — model {model}."
            };
        }
        catch (Exception)
        {
            return OfflineOllama(model,
                "Không kết nối được Ollama tại http://localhost:11434. Mở app Ollama (icon khay hệ thống) rồi thử lại.");
        }
    }

    private static AiStatusDto OfflineOllama(string model, string message) =>
        new()
        {
            ConfiguredForAi = true,
            IsOnline = false,
            Provider = "Ollama",
            Model = model,
            Message = message
        };

    private static string ResolveOllamaHost(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "http://localhost:11434";

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return "http://localhost:11434";

        return $"{uri.Scheme}://{uri.Authority}";
    }
}
