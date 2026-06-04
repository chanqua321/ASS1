using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Model.Helpers;

/// <summary>
/// So khớp tên chương để tránh upload trùng (Chương 1 vs Chương 1-Mở đầu…).
/// </summary>
public static class ChapterTitleHelper
{
    private static readonly Regex ChapterNumberPattern = new(
        @"(?:chuong|chương|chapter|ch)\s*[\.\-#:]?\s*(?<num>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool AreSimilar(string? titleA, string? titleB)
    {
        if (string.IsNullOrWhiteSpace(titleA) || string.IsNullOrWhiteSpace(titleB))
            return false;

        var keyA = BuildComparisonKey(titleA);
        var keyB = BuildComparisonKey(titleB);

        if (keyA.Length > 0 && keyA == keyB)
            return true;

        var numA = ExtractChapterNumber(titleA);
        var numB = ExtractChapterNumber(titleB);
        if (numA.HasValue && numB.HasValue && numA.Value == numB.Value)
            return true;

        var normA = Normalize(titleA);
        var normB = Normalize(titleB);
        if (normA.Length >= 4 && normB.Length >= 4 &&
            (normA.Contains(normB, StringComparison.Ordinal) || normB.Contains(normA, StringComparison.Ordinal)))
            return true;

        return false;
    }

    public static string BuildComparisonKey(string title)
    {
        var norm = Normalize(title);
        var num = ExtractChapterNumber(title);
        return num.HasValue ? $"ch{num.Value}|{norm}" : norm;
    }

    public static int? ExtractChapterNumber(string title)
    {
        var m = ChapterNumberPattern.Match(title.Trim());
        return m.Success && int.TryParse(m.Groups["num"].Value, out var n) ? n : null;
    }

    public static string Normalize(string title)
    {
        var t = title.Trim().ToLowerInvariant();
        t = RemoveDiacritics(t);
        t = Regex.Replace(t, @"[^a-z0-9\s]", " ");
        t = Regex.Replace(t, @"\s+", " ").Trim();
        return t;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
