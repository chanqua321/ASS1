using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Model.Enums;
using Service.Interfaces;
using UglyToad.PdfPig;

namespace Service.Implementations;

public class DocumentTextExtractor : IDocumentTextExtractor
{
    public Task<string> ExtractTextAsync(
        string filePath,
        DocumentFileType fileType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var text = fileType switch
        {
            DocumentFileType.Docx => ExtractDocx(filePath),
            DocumentFileType.Pdf => ExtractPdf(filePath),
            DocumentFileType.Pptx or DocumentFileType.Unknown => ExtractPlainOrPlaceholder(filePath, fileType),
            _ => ExtractPlainOrPlaceholder(filePath, fileType)
        };

        text = Normalize(text);
        return Task.FromResult(string.IsNullOrWhiteSpace(text)
            ? $"[Không trích xuất được nội dung từ {Path.GetFileName(filePath)}]"
            : text);
    }

    private static string ExtractDocx(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        var paragraphs = body.Descendants<Paragraph>()
            .Select(p => string.Join("", p.Descendants<Text>().Select(t => t.Text)).Trim())
            .Where(s => s.Length > 0);

        return string.Join("\n\n", paragraphs);
    }

    private static string ExtractPdf(string filePath)
    {
        var sb = new StringBuilder();
        using var pdf = PdfDocument.Open(filePath);
        foreach (var page in pdf.GetPages())
        {
            var pageText = page.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(pageText))
                sb.AppendLine(pageText);
        }

        return sb.ToString();
    }

    private static string ExtractPlainOrPlaceholder(string filePath, DocumentFileType fileType)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            if (bytes.Length > 4 && bytes[0] == 0x50 && bytes[1] == 0x4B)
            {
                return "[File PPTX/ZIP — hãy upload bản DOCX hoặc PDF để tóm tắt nội dung trong chat.]";
            }
        }
        catch
        {
            // ignore
        }

        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd();
        if (IsReadableText(content))
            return content;

        return $"[File {Path.GetFileName(filePath)} ({fileType}) — dùng PDF hoặc DOCX để chat tóm tắt.]";
    }

    private static bool IsReadableText(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length < 20)
            return false;

        var printable = content.Count(c => !char.IsControl(c) || c is '\n' or '\r' or '\t');
        return printable / (double)content.Length > 0.85;
    }

    private static string Normalize(string text) =>
        string.Join("\n", text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
}
