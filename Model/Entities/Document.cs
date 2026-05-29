using Model.Enums;

namespace Model.Entities;

public class Document
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DocumentFileType FileType { get; set; }
    public long FileSizeBytes { get; set; }
    public string FilePath { get; set; } = string.Empty;

    public int SubjectId { get; set; }
    public int? ChapterId { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public Subject Subject { get; set; } = null!;
    public Chapter? Chapter { get; set; }
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
