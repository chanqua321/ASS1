using Model.Enums;

namespace Service.DTOs;

public class DocumentListItemDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DocumentFileType FileType { get; set; }
    public DocumentStatus Status { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string? ChapterTitle { get; set; }
    public int ChunkCount { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
