using System.Text;
using System.Text.RegularExpressions;
using BusinessLogic.DTOs;

namespace BusinessLogic.Helpers;

/// <summary>
/// Giảm câu trả lời AI bịa (đặc biệt "xin lỗi không upload được file" khi tài liệu đã có trong RAG).
/// </summary>
public static class RagAnswerSanitizer
{
    public const string PlaceholderOnlyMessage =
        "File slide (PPTX) này chưa có nội dung chữ trong hệ thống để trả lời chính xác. " +
        "Giáo viên hãy upload thêm bản PDF hoặc DOCX cùng bài học, đợi trạng thái Indexed, rồi hỏi lại. " +
        "Bạn cũng có thể tải file gốc ở mục Tài liệu liên quan bên dưới.";

    /// <summary>Lưu vào Documents.Summary khi index PPTX không trích được chữ (không gọi Ollama).</summary>
    public const string PlaceholderDocumentSummary =
        "PPTX: chưa đọc được nội dung slide trong hệ thống. Upload PDF/DOCX cùng bài để chat và tóm tắt chính xác.";

    private static readonly string[] PlaceholderMarkers =
    [
        "[File PPTX",
        "[Không trích xuất được",
        "hãy upload bản DOCX",
        "dùng PDF hoặc DOCX để chat"
    ];

    private static readonly Regex UploadHallucinationLine = new(
        @"^(.*)(không\s+(thể|có)\s+(tải|upload)|chưa\s+(có\s+)?file|vì\s+không\s+có\s+file|since\s+no\s+file\s+was\s+uploaded|please\s+upload\s+the\s+file|hãy\s+upload\s+(file|lại)|tải\s+file\s+lên).*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsPlaceholderContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return true;

        var t = content.Trim();
        return PlaceholderMarkers.Any(m => t.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    public static bool AreChunksOnlyPlaceholders(IReadOnlyList<RetrievedChunkDto> chunks) =>
        chunks.Count > 0 && chunks.All(c => IsPlaceholderContent(c.Content));

    public static bool DocumentHasSearchableText(IEnumerable<string> chunkContents)
    {
        var list = chunkContents as IList<string> ?? chunkContents.ToList();
        return list.Count > 0 && list.Any(c => !IsPlaceholderContent(c));
    }

    public static string SanitizeAiAnswer(string answer, bool hadDocumentContext)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return answer;

        if (!hadDocumentContext)
            return answer.Trim();

        var lines = answer.Split('\n');
        var kept = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                kept.Add(line);
                continue;
            }

            if (UploadHallucinationLine.IsMatch(trimmed))
                continue;

            if (ContainsUploadContradiction(trimmed))
                continue;

            kept.Add(line);
        }

        var result = string.Join('\n', kept).Trim();
        return string.IsNullOrWhiteSpace(result) ? answer.Trim() : result;
    }

    private static bool ContainsUploadContradiction(string line)
    {
        var lower = line.ToLowerInvariant();
        var asksUpload = lower.Contains("upload") && (lower.Contains("file") || lower.Contains("tải"));
        var deniesFile = lower.Contains("không có file") || lower.Contains("no file was uploaded");
        var apologyUpload = lower.Contains("xin lỗi") &&
                            (lower.Contains("tải") || lower.Contains("upload"));
        return (asksUpload && deniesFile) || apologyUpload;
    }

    /// <summary>
    /// Khi context chỉ là placeholder, không gọi AI — tránh bullet bịa rồi mâu thuẫn "chưa upload".
    /// </summary>
    public static string BuildPlaceholderAwareMessage(IReadOnlyList<RetrievedChunkDto> chunks)
    {
        var fileName = chunks.FirstOrDefault()?.FileName ?? "tài liệu";
        var sb = new StringBuilder();
        sb.AppendLine(PlaceholderOnlyMessage);
        sb.AppendLine();
        sb.AppendLine($"Tài liệu liên quan: {fileName}.");
        return sb.ToString().Trim();
    }
}
