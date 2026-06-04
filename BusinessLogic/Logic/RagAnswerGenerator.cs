using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BusinessLogic.DTOs;
using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;
using BusinessLogic.Options;

namespace BusinessLogic.Logic;

/// <summary>
/// Một implementation RAG: Ollama/OpenAI khi bật, fallback trích xuất chunk local.
/// </summary>
public class RagAnswerGenerator : IRagAnswerGenerator
{
    private readonly HttpClient _http;
    private readonly AiModelOptions _ai;
    private readonly RagChatOptions _rag;
    private readonly ILogger<RagAnswerGenerator> _logger;

    public RagAnswerGenerator(
        HttpClient http,
        IOptions<AiModelOptions> ai,
        IOptions<RagChatOptions> rag,
        ILogger<RagAnswerGenerator> logger)
    {
        _http = http;
        _ai = ai.Value;
        _rag = rag.Value;
        _logger = logger;
    }

    public async Task<(string Answer, bool FromDocuments)> GenerateAsync(
        string question,
        IReadOnlyList<RetrievedChunkDto> chunks,
        IReadOnlyList<string> recentConversation,
        bool includeCitationHints = false,
        bool isSummaryQuestion = false,
        CancellationToken cancellationToken = default)
    {
        // Generate:
        // - Chuẩn bị/short-circuit (ví dụ: không có chunk phù hợp)
        // - Chọn chunk liên quan (Select / SelectForSummary)
        // - Nếu AI sẵn sàng: gọi /chat/completions (Ollama/OpenAI)
        // - Nếu AI lỗi/offline: fallback trả lời local theo excerpt
        var prepared = RagChunkSelector.TryPrepare(question, chunks, _rag);
        if (prepared is { } early)
            return early;

        var relevant = isSummaryQuestion
            ? RagChunkSelector.SelectForSummary(chunks, _rag)
            : RagChunkSelector.Select(question, chunks, _rag);

        if (isSummaryQuestion && relevant.Count > 0)
            return await GenerateSummaryAsync(question, relevant, recentConversation, includeCitationHints, cancellationToken);

        if (_ai.Enabled && _ai.IsRemoteAiConfigured())
        {
            try
            {
                // Thành công: answer từ AI (có thể dựa vào context chunk)
                var answer = await CallChatCompletionsAsync(
                    question, relevant, recentConversation, includeCitationHints, isSummaryQuestion, cancellationToken);
                return (answer, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI ({Provider}) failed, using local excerpt mode.", _ai.Provider);
            }
        }

        // Fallback: không gọi AI, trả lời dựa trên các đoạn tài liệu liên quan.
        return (BuildLocalAnswer(relevant, _rag.MaxExcerptLength), true);
    }

    private async Task<(string Answer, bool FromDocuments)> GenerateSummaryAsync(
        string question,
        IReadOnlyList<RetrievedChunkDto> relevant,
        IReadOnlyList<string> recentConversation,
        bool includeCitationHints,
        CancellationToken cancellationToken)
    {
        if (_ai.Enabled && _ai.IsRemoteAiConfigured())
        {
            try
            {
                var answer = await CallChatCompletionsAsync(
                    question,
                    relevant,
                    recentConversation,
                    includeCitationHints,
                    isSummary: true,
                    cancellationToken);
                return (StripSummaryBoilerplate(answer), true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI summary failed, using local summary.");
            }
        }

        return (BuildLocalSummary(relevant), true);
    }

    private static string BuildLocalSummary(IReadOnlyList<RetrievedChunkDto> relevant)
    {
        var fileName = relevant[0].FileName;
        var builder = new StringBuilder();
        builder.AppendLine($"Tóm tắt nhanh file {fileName} (theo các đoạn chính):");
        builder.AppendLine();

        foreach (var chunk in relevant.Take(6))
        {
            var line = chunk.Content.Length > 280 ? chunk.Content[..280] + "..." : chunk.Content;
            builder.AppendLine($"• {line}");
        }

        return builder.ToString().Trim();
    }

    private static string BuildLocalAnswer(IReadOnlyList<RetrievedChunkDto> relevant, int maxExcerptLength)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Theo tài liệu bạn đã tải lên, mình tóm lại như sau:");
        builder.AppendLine();

        for (var i = 0; i < relevant.Count; i++)
        {
            var chunk = relevant[i];
            var excerpt = chunk.Content.Length <= maxExcerptLength
                ? chunk.Content
                : chunk.Content[..maxExcerptLength] + "...";
            builder.AppendLine($"[{i + 1}] {excerpt}");
            builder.AppendLine(
                $"    (Nguồn: {chunk.FileName}, môn {chunk.SubjectCode}, độ khớp: {chunk.Score:P0})");
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private async Task<string> CallChatCompletionsAsync(
        string question,
        IReadOnlyList<RetrievedChunkDto> relevant,
        IReadOnlyList<string> recentConversation,
        bool includeCitationHints,
        bool isSummary,
        CancellationToken cancellationToken)
    {
        // Call AI:
        // - Build context block từ chunk (cắt theo MaxExcerptLength)
        // - Tạo system prompt + messages
        // - POST {BaseUrl}/chat/completions
        var excerptLen = isSummary ? _rag.SummaryMaxExcerptLength : _rag.MaxExcerptLength;
        var context = RagChunkSelector.BuildContextBlock(relevant, excerptLen);

        var citationRule = includeCitationHints && !isSummary
            ? "Khi cần, ghi [1], [2]... khớp số nguồn trong ngữ cảnh."
            : "Trả lời tự nhiên, không bắt buộc ghi số nguồn [1], [2] trong câu trả lời.";

        var taskRule = isSummary
            ? "TÓM TẮT khoảng 100–300 từ (tiếng Việt): 2-3 câu mở đầu + 4-6 bullet ý chính + 1 câu kết. Không nhắc Ollama/hệ thống, không ghi chú _(Còn X đoạn...)_. "
            : "";

        var systemPrompt = isSummary
            ? "Bạn là trợ lý học tập. Trả lời tiếng Việt ngắn gọn, dựa chỉ vào NGỮ CẢNH. " + taskRule
            : "Bạn là trợ lý học tập thân thiện. Trả lời bằng tiếng Việt tự nhiên, dễ nghe, tránh thuật ngữ kỹ thuật (không dùng RAG, index, hệ thống...). " +
              taskRule +
              "Chỉ dựa vào phần NGỮ CẢNH TÀI LIỆU bên dưới; không bịa thêm. " +
              "Nếu thiếu thông tin, nói nhẹ nhàng kiểu: \"Trong tài liệu hiện có mình chưa thấy phần này, bạn thử hỏi cụ thể hơn nhé.\" " +
              citationRule;

        var messages = new List<object> { new { role = "system", content = systemPrompt } };

        if (!isSummary)
        {
            foreach (var line in recentConversation.TakeLast(4))
                messages.Add(new { role = "user", content = line });
        }

        messages.Add(new
        {
            role = "user",
            content = isSummary
                ? $"NGỮ CẢNH:\n{context}\n\nTóm tắt: {question}"
                : $"NGỮ CẢNH TÀI LIỆU:\n{context}\n\nCÂU HỎI: {question}"
        });

        var maxTokens = isSummary ? _ai.SummaryMaxTokens : _ai.MaxTokens;
        var payload = new
        {
            model = _ai.Model,
            messages,
            temperature = isSummary ? Math.Min(_ai.Temperature, 0.2) : _ai.Temperature,
            max_tokens = maxTokens
        };

        var baseUrl = _ai.BaseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        if (_ai.RequiresBearerToken())
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _ai.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"AI API error {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return string.IsNullOrWhiteSpace(content)
            ? "Model AI không trả về nội dung."
            : content.Trim();
    }

    private static string StripSummaryBoilerplate(string answer)
    {
        var filtered = answer.Split('\n')
            .Where(line =>
            {
                var t = line.Trim();
                if (t.Length == 0) return true;
                return !(t.Contains("đoạn khác", StringComparison.OrdinalIgnoreCase) &&
                         (t.Contains("Ollama", StringComparison.OrdinalIgnoreCase) ||
                          t.Contains("bật", StringComparison.OrdinalIgnoreCase) ||
                          t.StartsWith("_(")));
            })
            .ToList();

        return string.Join('\n', filtered).Trim();
    }
}
