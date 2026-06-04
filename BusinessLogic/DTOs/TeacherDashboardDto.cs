namespace BusinessLogic.DTOs;

public class TeacherDashboardDto
{
    public int SubjectCount { get; set; }
    public int DocumentCount { get; set; }
    public int ChunkCount { get; set; }
    public int StudentChatMessageCount { get; set; }
    public IReadOnlyList<TeacherRecentDocumentDto> RecentDocuments { get; set; } =
        Array.Empty<TeacherRecentDocumentDto>();
}

public class TeacherRecentDocumentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string? ChapterTitle { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? SummaryPreview { get; set; }
}
