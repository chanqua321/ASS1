using System.Text;
using System.Text.RegularExpressions;

namespace BusinessLogic.Helpers;

/// <summary>
/// Nhận diện câu chào hỏi / hội thoại thường — không cần RAG tài liệu.
/// </summary>
public static class ConversationalIntentHelper
{
    public static bool TryGetReply(string question, out string reply)
    {
        reply = string.Empty;
        if (string.IsNullOrWhiteSpace(question))
            return false;

        if (LooksLikeDocumentQuestion(question))
            return false;

        var q = Normalize(question);

        if (MatchesAny(q,
                "ban la ai", "bạn là ai", "may la ai", "mày là ai",
                "who are you", "what are you", "gioi thieu", "giới thiệu",
                "ten ban la gi", "tên bạn là gì", "ban la gi", "bạn là gì"))
        {
            reply = BuildIdentityReply();
            return true;
        }

        if (MatchesAny(q,
                "giup gi", "giúp gì", "lam duoc gi", "làm được gì",
                "ban lam gi", "bạn làm gì", "huong dan", "hướng dẫn",
                "help", "what can you do", "chuc nang", "chức năng"))
        {
            reply = BuildHelpReply();
            return true;
        }

        if (MatchesAny(q,
                "cam on", "cảm ơn", "thank", "thanks", "camon"))
        {
            reply = "Không có chi! Cần hỏi thêm về bài hay tài liệu thì cứ nhắn mình nhé.";
            return true;
        }

        if (MatchesAny(q,
                "tam biet", "tạm biệt", "bye", "goodbye", "see you"))
        {
            reply = "Tạm biệt nhé! Lúc nào cần hỏi bài cứ quay lại chat với mình.";
            return true;
        }

        if (MatchesAny(q,
                "khoe khong", "khỏe không", "how are you", "the nao", "thế nào",
                "co khoe khong", "có khỏe không"))
        {
            reply =
                "Mình vẫn khỏe, cảm ơn bạn đã hỏi! " +
                "Bạn muốn hỏi nội dung bài nào, hay nhờ tóm tắt một file cụ thể không?";
            return true;
        }

        if (IsGreeting(q))
        {
            reply = BuildGreetingReply();
            return true;
        }

        return false;
    }

    private static bool LooksLikeDocumentQuestion(string question)
    {
        var q = Normalize(question);

        if (RagChunkSelector.IsSummaryQuestion(question))
            return true;

        if (RagChunkSelector.IsDocumentInventoryQuestion(question))
            return true;

        if (Regex.IsMatch(q, @"\.(pdf|docx?|pptx?|ppt)\b"))
            return true;

        return MatchesAny(q,
            "tom tat", "tóm tắt", "tom lược", "tóm lược",
            "chuong ", "chương ", "bai ", "bài ",
            "tai lieu", "tài liệu", "document", "file ",
            "noi dung", "nội dung", "giai thich", "giải thích",
            "trong sach", "trong sách", "theo tai lieu", "theo tài liệu",
            "upload", "index", "download", "tai ve", "tải về",
            "tong hop", "tổng hợp", "summarize", "summary");
    }

    private static bool IsGreeting(string normalized)
    {
        if (normalized.Length > 60)
            return false;

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 8)
            return false;

        var greetingWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "hi", "hello", "hey", "yo", "alo", "chao", "xin", "chào",
            "good", "morning", "afternoon", "evening", "day"
        };

        var hasGreeting = tokens.Any(t => greetingWords.Contains(t));
        if (!hasGreeting)
            return false;

        // "chào bạn", "xin chào", "hi there"
        if (tokens.Length <= 4)
            return true;

        // "hi bạn là ai" đã xử lý ở nhánh identity (ưu tiên trước)
        return tokens.Length <= 6 && !LooksLikeDocumentQuestion(normalized);
    }

    private static string BuildGreetingReply() =>
        "Chào bạn! Mình là trợ lý học tập trên Assigment1 — giúp bạn hỏi đáp và tóm tắt theo file bài đã tải lên (PDF, Word, slide). " +
        "Cứ hỏi nội dung bài, hoặc nhắn \"bạn là ai\" / \"giúp gì\" nếu muốn biết thêm nhé.";

    private static string BuildIdentityReply()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Mình là trợ lý học tập trên Assigment1 — bạn cứ coi mình như người giúp đọc và tra bài theo tài liệu bạn đã đưa vào.");
        sb.AppendLine();
        sb.AppendLine("Mình có thể giúp bạn:");
        sb.AppendLine("• Trả lời câu hỏi dựa trên nội dung file bài học");
        sb.AppendLine("• Tóm tắt một file (ví dụ: \"Tóm tắt HireMate.docx\")");
        sb.AppendLine("• Cho biết bạn đang có những tài liệu nào và gửi link tải khi cần");
        sb.AppendLine("• Chào hỏi, trò chuyện và hướng dẫn cách dùng chat");
        sb.AppendLine();
        sb.Append("Bạn hỏi tự nhiên như nhắn tin bình thường là được — mình sẽ trả lời cho sát ý bạn.");
        return sb.ToString().Trim();
    }

    private static string BuildHelpReply() =>
        "Để dùng chat cho dễ nhớ, bạn có thể:\n" +
        "1. Hỏi nội dung bài — ví dụ: \"Giải thích chương 2 trong file X\"\n" +
        "2. Nhờ tóm tắt — ví dụ: \"Tóm tắt file ABC.docx\"\n" +
        "3. Hỏi đang có tài liệu gì — ví dụ: \"Môn này có tài liệu không?\"\n" +
        "4. Chào hỏi bình thường — \"Hi\", \"Bạn là ai?\", \"Cảm ơn\"...\n\n" +
        "Lưu ý nhỏ: trước tiên cần tải file lên và đợi xử lý xong, rồi mới hỏi sâu về nội dung bài nhé.";

    private static bool MatchesAny(string normalized, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var p = Normalize(pattern);
            if (normalized.Contains(p, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string Normalize(string text)
    {
        var s = text.Trim().ToLowerInvariant();
        s = RemoveDiacritics(s);
        s = Regex.Replace(s, @"[^\w\s]", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
