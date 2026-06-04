namespace BusinessLogic.DTOs;

public class DocumentUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public Stream FileStream { get; set; } = Stream.Null;

    /// <summary>Môn học có sẵn (khi không nhập môn mới).</summary>
    public int? SubjectId { get; set; }

    /// <summary>Tên môn/topic mới do người dùng nhập.</summary>
    public string? NewSubjectName { get; set; }

    public string? NewSubjectCode { get; set; }

    public int? ChapterId { get; set; }

    /// <summary>Tên chương mới do người dùng nhập.</summary>
    public string? NewChapterTitle { get; set; }

    public int UploadedByUserId { get; set; }
}

