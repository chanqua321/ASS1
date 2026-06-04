namespace Model.IRepository;

public interface IAnalyticsRepository
{
    Task<int> CountSubjectsForTeacherAsync(int teacherUserId, CancellationToken cancellationToken = default);
    Task<int> CountDocumentsForTeacherAsync(int teacherUserId, CancellationToken cancellationToken = default);
    Task<int> CountChunksForTeacherAsync(int teacherUserId, CancellationToken cancellationToken = default);
    Task<int> CountStudentChatMessagesForTeacherAsync(int teacherUserId, CancellationToken cancellationToken = default);
    Task<List<TeacherRecentDocumentRow>> GetRecentDocumentsForTeacherAsync(
        int teacherUserId,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountUsersAsync(CancellationToken cancellationToken = default);
    Task<int> CountUsersByRoleAsync(string role, CancellationToken cancellationToken = default);
    Task<int> CountSubjectsAsync(CancellationToken cancellationToken = default);
    Task<int> CountDocumentsAsync(CancellationToken cancellationToken = default);
    Task<int> CountChatSessionsAsync(CancellationToken cancellationToken = default);
    Task<int> CountUserQuestionsAsync(CancellationToken cancellationToken = default);
    Task<List<ChartCountRow>> GetChatCountByDayAsync(int days, CancellationToken cancellationToken = default);
    Task<List<ChartCountRow>> GetDocumentUploadCountByMonthAsync(int months, CancellationToken cancellationToken = default);
    Task<List<SubjectAccessRow>> GetTopSubjectsByChatSessionsAsync(int take, CancellationToken cancellationToken = default);
}

public sealed class TeacherRecentDocumentRow
{
    public int Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string? ChapterTitle { get; init; }
    public DateTime UploadedAt { get; init; }
    public string? SummaryPreview { get; init; }
}

public sealed class ChartCountRow
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class SubjectAccessRow
{
    public string SubjectCode { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public int SessionCount { get; init; }
}
