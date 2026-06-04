using BusinessLogic.DTOs;
using BusinessLogic.Options;

namespace BusinessLogic.Helpers;

public static class RagChunkSelector
{
    public const string NoDocumentsMessage =
        "Hiện tôi chưa có tài liệu nào để tra cứu. " +
        "Bạn upload file PDF/DOCX (hoặc slide) và đợi xử lý xong, rồi hỏi lại nhé.";

    public const string NoRelevantChunksMessage =
        "Tôi chưa tìm thấy đoạn nào khớp với câu hỏi trong các tài liệu hiện có. " +
        "Bạn thử hỏi rõ hơn theo tên file hoặc nội dung chương/bài nhé.";

    /// <summary>
    /// Trả về câu trả lời sớm (inventory / không có chunk); null nếu cần sinh câu trả lời tiếp.
    /// </summary>
    public static (string Answer, bool FromDocuments)? TryPrepare(
        string question,
        IReadOnlyList<RetrievedChunkDto> chunks,
        RagChatOptions options)
    {
        if (chunks.Count == 0)
            return (NoDocumentsMessage, false);

        var relevant = IsSummaryQuestion(question)
            ? SelectForSummary(chunks, options)
            : Select(question, chunks, options);

        if (relevant.Count == 0)
            return (NoRelevantChunksMessage, false);

        if (RagAnswerSanitizer.AreChunksOnlyPlaceholders(relevant))
            return (RagAnswerSanitizer.BuildPlaceholderAwareMessage(relevant), true);

        if (IsDocumentInventoryQuestion(question))
            return (BuildInventoryAnswer(relevant), true);

        return null;
    }

    public static bool IsSummaryQuestion(string question)
    {
        var q = question.ToLowerInvariant();
        return q.Contains("tóm tắt") || q.Contains("tom tat") ||
               q.Contains("tóm lược") || q.Contains("tom luoc") ||
               q.Contains("summarize") || q.Contains("summary") ||
               q.Contains("tổng hợp nội dung") || q.Contains("tong hop noi dung") ||
               q.Contains("nội dung chính") || q.Contains("noi dung chinh");
    }

    public static IReadOnlyList<RetrievedChunkDto> SelectForSummary(
        IReadOnlyList<RetrievedChunkDto> chunks,
        RagChatOptions options)
    {
        if (chunks.Count == 0)
            return Array.Empty<RetrievedChunkDto>();

        var maxChunks = Math.Max(1, options.SummaryMaxChunks);
        var byDoc = chunks.GroupBy(c => c.DocumentId).ToList();

        if (byDoc.Count == 1)
        {
            var ordered = byDoc[0].OrderBy(c => c.ChunkId).ToList();
            if (ordered.Count <= maxChunks)
                return ordered;

            return options.SummarySampleEvenly
                ? SampleEvenly(ordered, maxChunks)
                : ordered.Take(maxChunks).ToList();
        }

        return chunks
            .OrderByDescending(c => c.Score)
            .Take(maxChunks)
            .ToList();
    }

    private static List<RetrievedChunkDto> SampleEvenly(
        IReadOnlyList<RetrievedChunkDto> ordered,
        int maxChunks)
    {
        if (ordered.Count <= maxChunks)
            return ordered.ToList();

        if (maxChunks == 1)
            return [ordered[0]];

        var result = new List<RetrievedChunkDto>(maxChunks);
        var step = (double)(ordered.Count - 1) / (maxChunks - 1);

        for (var i = 0; i < maxChunks; i++)
        {
            var idx = (int)Math.Round(i * step);
            var chunk = ordered[idx];
            if (result.All(c => c.ChunkId != chunk.ChunkId))
                result.Add(chunk);
        }

        return result.Count >= maxChunks
            ? result
            : ordered.Take(maxChunks).ToList();
    }

    public static IReadOnlyList<RetrievedChunkDto> Select(
        string question,
        IReadOnlyList<RetrievedChunkDto> chunks,
        RagChatOptions options)
    {
        if (chunks.Count == 0)
            return Array.Empty<RetrievedChunkDto>();

        var relevant = chunks
            .Where(c => c.Score >= options.MinSimilarityScore)
            .ToList();

        if (relevant.Count == 0)
        {
            var best = chunks.MaxBy(c => c.Score)?.Score ?? 0;
            if (best >= options.FallbackMinScore || IsDocumentInventoryQuestion(question))
            {
                relevant = chunks
                    .OrderByDescending(c => c.Score)
                    .Take(options.TopK)
                    .ToList();
            }
        }

        return relevant;
    }

    public static bool IsDocumentInventoryQuestion(string question)
    {
        var q = question.ToLowerInvariant();
        return q.Contains("có tài liệu") || q.Contains("co tai lieu") ||
               q.Contains("tài liệu về môn") || q.Contains("tai lieu ve mon") ||
               q.Contains("có document") || q.Contains("danh sách tài liệu") ||
               (q.Contains("môn") && (q.Contains("có") || q.Contains("co")));
    }

    public static string BuildInventoryAnswer(IReadOnlyList<RetrievedChunkDto> chunks)
    {
        var docs = chunks
            .GroupBy(c => c.DocumentId)
            .Select(g => g.First())
            .ToList();

        var intro = docs.Count switch
        {
            1 => "Hiện tôi có 1 tài liệu bạn có thể hỏi:",
            _ => $"Hiện tôi có {docs.Count} tài liệu bạn có thể hỏi:"
        };

        var lines = new List<string> { intro, "" };

        foreach (var d in docs)
        {
            var subject = FormatSubjectLabel(d.SubjectCode, d.SubjectName);
            var chapter = string.IsNullOrWhiteSpace(d.ChapterTitle) ? null : d.ChapterTitle.Trim();

            if (chapter is not null)
                lines.Add($"• {d.FileName} — {subject}, chương \"{chapter}\"");
            else
                lines.Add($"• {d.FileName} — {subject}");
        }

        lines.Add("");
        lines.Add(docs.Count == 1
            ? "Bạn muốn tóm tắt hay hỏi chi tiết phần nào trong file này?"
            : "Cứ hỏi tôi nội dung cụ thể hoặc nhờ tóm tắt file bạn quan tâm nhé.");

        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static string FormatSubjectLabel(string code, string name)
    {
        var c = code?.Trim() ?? "";
        var n = name?.Trim() ?? "";

        if (string.IsNullOrEmpty(c) && string.IsNullOrEmpty(n))
            return "chưa gắn môn";
        if (string.IsNullOrEmpty(c))
            return $"môn {n}";
        if (string.IsNullOrEmpty(n) || string.Equals(c, n, StringComparison.OrdinalIgnoreCase))
            return $"môn {c}";
        return $"môn {c} ({n})";
    }

    public static string BuildContextBlock(IReadOnlyList<RetrievedChunkDto> chunks, int maxExcerptLength)
    {
        var parts = new List<string>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var c = chunks[i];
            var excerpt = c.Content.Length <= maxExcerptLength
                ? c.Content
                : c.Content[..maxExcerptLength] + "...";
            parts.Add(
                $"[{i + 1}] File: {c.FileName}, Môn: {c.SubjectCode}, Chương: {c.ChapterTitle ?? "—"}\n{excerpt}");
        }

        return string.Join("\n\n", parts);
    }
}
