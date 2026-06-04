using Model.Enums;

namespace BusinessLogic.DTOs;

public class DocumentListItemDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DocumentFileType FileType { get; set; }
    public DocumentStatus Status { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectDisplay =>
        string.IsNullOrWhiteSpace(SubjectCode) ? SubjectName : $"{SubjectCode} — {SubjectName}";
    public string? ChapterTitle { get; set; }
    public int ChunkCount { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = "bg-secondary";
    public string StatusIcon { get; set; } = "bi-clock";
    public bool IsIndexed { get; set; }
    public string FileTypeLabel { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTime? SummaryGeneratedAt { get; set; }
    public string? SummaryPreview { get; set; }
    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);
    /// <summary>False khi chunk chỉ là ghi chú PPTX/placeholder — chat RAG không dùng được.</summary>
    public bool HasSearchableText { get; set; } = true;
}
