using Model.Enums;
using Service.Interfaces;

namespace Service.Implementations;

/// <summary>
/// Trích xuất text đơn giản — thay bằng PdfPig, OpenXML, v.v. khi triển khai thật.
/// </summary>
public class StubDocumentTextExtractor : IDocumentTextExtractor
{
    public async Task<string> ExtractTextAsync(string filePath, DocumentFileType fileType, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(content) && !content.Any(c => char.IsControl(c) && c != '\n' && c != '\r'))
            return content;

        return $"[Nội dung mẫu từ file {Path.GetFileName(filePath)} — loại {fileType}. " +
               "Tích hợp thư viện đọc PDF/DOCX/PPTX để trích xuất text thực tế.]";
    }
}
