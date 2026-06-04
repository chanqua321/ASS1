using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BusinessLogic.DTOs;
using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;
using BusinessLogic.Options;
using Model.Entities;
using Model.Enums;
using Model.IRepository;
using Model.IUnitOfWork;

namespace BusinessLogic.Logic;

public partial class QuizService : IQuizService
{
    private readonly IDocumentQuizRepository _quizzes;
    private readonly IDocumentRepository _documents;
    private readonly IChunkRepository _chunks;
    private readonly IUnitOfWork _uow;
    private readonly HttpClient _http;
    private readonly AiModelOptions _ai;
    private readonly RagChatOptions _rag;
    private readonly ILogger<QuizService> _logger;

    public QuizService(
        IDocumentQuizRepository quizzes,
        IDocumentRepository documents,
        IChunkRepository chunks,
        IUnitOfWork uow,
        HttpClient http,
        IOptions<AiModelOptions> ai,
        IOptions<RagChatOptions> rag,
        ILogger<QuizService> logger)
    {
        _quizzes = quizzes;
        _documents = documents;
        _chunks = chunks;
        _uow = uow;
        _http = http;
        _ai = ai.Value;
        _rag = rag.Value;
        _logger = logger;
    }

    public async Task<(bool Success, string Error, DocumentQuizDto? Quiz)> GenerateAsync(
        int documentId,
        int userId,
        int questionCount,
        CancellationToken cancellationToken = default)
    {
        questionCount = ClampQuestionCount(questionCount);
        var doc = await _documents.GetByIdWithDetailsAsync(documentId, cancellationToken);
        if (doc is null)
            return (false, "Tài liệu không tồn tại.", null);
        if (doc.Status != DocumentStatus.Indexed)
            return (false, "Tài liệu chưa Indexed — không thể sinh quiz.", null);

        var chunks = await _chunks.GetByDocumentIdOrderedAsync(documentId, cancellationToken);
        if (chunks.Count == 0)
            return (false, "Không có nội dung chunk để sinh quiz.", null);

        var selected = RagChunkSelector.SelectForSummary(
            chunks.Select(c => new RetrievedChunkDto
            {
                ChunkId = c.Id,
                DocumentId = c.DocumentId,
                FileName = doc.FileName,
                Content = c.Content,
                Score = 1.0
            }).ToList(),
            _rag);

        var questions = await GenerateQuestionsAsync(doc.FileName, selected, questionCount, cancellationToken);
        if (questions.Count == 0)
            return (false, "AI không tạo được câu hỏi. Kiểm tra Ollama hoặc thử lại.", null);

        var entity = new DocumentQuiz
        {
            DocumentId = documentId,
            CreatedByUserId = userId,
            QuestionsJson = JsonSerializer.Serialize(questions),
            CreatedAt = DateTime.UtcNow
        };
        await _quizzes.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return (true, "", await MapFromEntityAsync(entity.Id, cancellationToken));
    }

    private async Task<DocumentQuizDto?> MapFromEntityAsync(int id, CancellationToken cancellationToken)
    {
        var q = await _quizzes.GetByIdAsync(id, cancellationToken);
        if (q is null) return null;
        return MapQuiz(q, q.Document?.FileName ?? "", q.CreatedByUser?.Email ?? "");
    }

    public async Task<DocumentQuizDto?> GetLatestAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var q = await _quizzes.GetLatestByDocumentIdAsync(documentId, cancellationToken);
        if (q is null) return null;
        var doc = await _documents.GetByIdWithDetailsAsync(documentId, cancellationToken);
        return MapQuiz(q, doc?.FileName ?? "", q.CreatedByUser?.Email ?? "");
    }

    public async Task<IReadOnlyList<DocumentQuizDto>> GetHistoryAsync(int documentId, int take = 10, CancellationToken cancellationToken = default)
    {
        var list = await _quizzes.GetHistoryByDocumentIdAsync(documentId, take, cancellationToken);
        var doc = await _documents.GetByIdWithDetailsAsync(documentId, cancellationToken);
        var fileName = doc?.FileName ?? "";
        return list.Select(q => MapQuiz(q, fileName, q.CreatedByUser?.Email ?? "")).ToList();
    }

    public Task<DocumentQuizDto?> GetByIdAsync(int quizId, CancellationToken cancellationToken = default) =>
        MapFromEntityAsync(quizId, cancellationToken);

    public string BuildExportContent(DocumentQuizDto quiz, string format)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Quiz — {quiz.DocumentFileName}");
        sb.AppendLine($"Generated: {quiz.CreatedAt.ToLocalTime():g}");
        sb.AppendLine();

        for (var i = 0; i < quiz.Questions.Count; i++)
        {
            var q = quiz.Questions[i];
            sb.AppendLine($"Question {i + 1}:");
            sb.AppendLine(q.Question);
            sb.AppendLine();
            for (var j = 0; j < q.Options.Count; j++)
            {
                var label = (char)('A' + j);
                sb.AppendLine($"{label}. {q.Options[j]}");
            }
            sb.AppendLine();
            sb.AppendLine($"Correct Answer: {(char)('A' + q.CorrectIndex)}");
            sb.AppendLine($"Explanation: {q.Explanation}");
            sb.AppendLine(new string('-', 40));
        }

        if (format.Equals("word", StringComparison.OrdinalIgnoreCase))
        {
            return $"<html><head><meta charset='utf-8'></head><body><pre>{System.Net.WebUtility.HtmlEncode(sb.ToString())}</pre></body></html>";
        }

        return sb.ToString();
    }

    private int ClampQuestionCount(int count) =>
        Math.Clamp(count, _rag.QuizMinQuestionCount, _rag.QuizMaxQuestionCount);

    private async Task<List<QuizQuestionDto>> GenerateQuestionsAsync(
        string fileName,
        IReadOnlyList<RetrievedChunkDto> chunks,
        int questionCount,
        CancellationToken cancellationToken)
    {
        var context = RagChunkSelector.BuildContextBlock(chunks, _rag.SummaryMaxExcerptLength);
        var local = BuildLocalQuiz(chunks, questionCount);
        if (!_ai.Enabled || !_ai.IsRemoteAiConfigured())
            return local;

        var minAccept = Math.Max(2, questionCount / 3);

        try
        {
            var prompt = $@"NGỮ CẢNH TÀI LIỆU ({fileName}):
{context}

Tạo đúng {questionCount} câu hỏi trắc nghiệm tiếng Anh hoặc Việt (ưu tiên ngôn ngữ của tài liệu).
Mỗi câu có 4 đáp án A-D, 1 đáp án đúng, giải thích ngắn.

CHỈ trả về JSON array, KHÔNG markdown:
[
  {{""question"":""..."",""options"":[""..."",""..."",""..."",""...""],""correctIndex"":0,""explanation"":""...""}}
]
correctIndex: 0-3.";

            var raw = await CallAiAsync(prompt, questionCount, cancellationToken);
            var parsed = ParseQuestionsJson(raw, questionCount);
            return parsed.Count >= minAccept ? parsed : local;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quiz AI failed, using local fallback.");
            return local;
        }
    }

    private async Task<string> CallAiAsync(string userPrompt, int questionCount, CancellationToken cancellationToken)
    {
        var maxTokens = Math.Clamp(questionCount * 220, 1024, 8192);
        var payload = new
        {
            model = _ai.Model,
            messages = new object[]
            {
                new { role = "system", content = "Bạn tạo quiz từ tài liệu. Chỉ trả JSON hợp lệ." },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.4,
            max_tokens = maxTokens
        };

        var baseUrl = _ai.BaseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        if (_ai.RequiresBearerToken())
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _ai.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(body);

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "[]";
    }

    private static List<QuizQuestionDto> ParseQuestionsJson(string raw, int questionCount)
    {
        var json = raw.Trim();
        var match = JsonArrayRegex().Match(json);
        if (match.Success)
            json = match.Value;

        try
        {
            var items = JsonSerializer.Deserialize<List<QuizQuestionDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return items?
                .Where(q => q.Options.Count >= 2 && q.CorrectIndex >= 0 && q.CorrectIndex < q.Options.Count)
                .Take(questionCount)
                .ToList() ?? new List<QuizQuestionDto>();
        }
        catch
        {
            return new List<QuizQuestionDto>();
        }
    }

    private static List<QuizQuestionDto> BuildLocalQuiz(IReadOnlyList<RetrievedChunkDto> chunks, int questionCount)
    {
        var result = new List<QuizQuestionDto>();
        var take = Math.Min(questionCount, chunks.Count);
        for (var i = 0; i < take; i++)
        {
            var excerpt = chunks[i].Content.Length > 200 ? chunks[i].Content[..200] + "..." : chunks[i].Content;
            result.Add(new QuizQuestionDto
            {
                Question = $"According to the document, which statement best matches section {i + 1}?",
                Options = new List<string>
                {
                    excerpt,
                    "This topic is not covered in the material.",
                    "The document only discusses unrelated subjects.",
                    "None of the above."
                },
                CorrectIndex = 0,
                Explanation = "The correct choice is taken from the indexed chunk content."
            });
        }
        return result;
    }

    private static DocumentQuizDto MapQuiz(DocumentQuiz entity, string fileName, string createdByEmail)
    {
        List<QuizQuestionDto> questions;
        try
        {
            questions = JsonSerializer.Deserialize<List<QuizQuestionDto>>(entity.QuestionsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<QuizQuestionDto>();
        }
        catch
        {
            questions = new List<QuizQuestionDto>();
        }

        return new DocumentQuizDto
        {
            Id = entity.Id,
            DocumentId = entity.DocumentId,
            DocumentFileName = fileName,
            CreatedByEmail = createdByEmail,
            CreatedAt = entity.CreatedAt,
            Questions = questions
        };
    }

    [GeneratedRegex(@"\[[\s\S]*\]")]
    private static partial Regex JsonArrayRegex();
}
