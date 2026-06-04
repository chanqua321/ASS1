using BusinessLogic.DTOs;
using BusinessLogic.IBusinessLogic;
using Model.IRepository;

namespace BusinessLogic.Logic;

public class DashboardService : IDashboardService
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IDocumentService _documents;

    public DashboardService(IAnalyticsRepository analytics, IDocumentService documents)
    {
        _analytics = analytics;
        _documents = documents;
    }

    public async Task<TeacherDashboardDto> GetTeacherDashboardAsync(
        int teacherUserId,
        CancellationToken cancellationToken = default)
    {
        var allDocs = await _documents.GetProcessedDocumentsAsync(
            subjectId: null,
            teacherUserId: teacherUserId,
            viewerUserId: teacherUserId,
            viewerIsAdmin: false,
            cancellationToken);

        var recent = allDocs
            .OrderByDescending(d => d.UploadedAt)
            .Take(5)
            .ToList();

        return new TeacherDashboardDto
        {
            SubjectCount = await _analytics.CountSubjectsForTeacherAsync(teacherUserId, cancellationToken),
            DocumentCount = allDocs.Count,
            ChunkCount = allDocs.Sum(d => d.ChunkCount),
            StudentChatMessageCount = await _analytics.CountStudentChatMessagesForTeacherAsync(teacherUserId, cancellationToken),
            RecentDocuments = recent.Select(d => new TeacherRecentDocumentDto
            {
                Id = d.Id,
                FileName = d.FileName,
                SubjectName = string.IsNullOrWhiteSpace(d.SubjectCode) ? d.SubjectName : d.SubjectDisplay,
                ChapterTitle = d.ChapterTitle,
                UploadedAt = d.UploadedAt,
                SummaryPreview = d.SummaryPreview
            }).ToList()
        };
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        const int chatDays = 14;
        const int uploadMonths = 6;
        const int topSubjects = 8;

        var chatByDay = await _analytics.GetChatCountByDayAsync(chatDays, cancellationToken);
        var uploadsByMonth = await _analytics.GetDocumentUploadCountByMonthAsync(uploadMonths, cancellationToken);
        var top = await _analytics.GetTopSubjectsByChatSessionsAsync(topSubjects, cancellationToken);

        return new AdminDashboardDto
        {
            TotalUsers = await _analytics.CountUsersAsync(cancellationToken),
            TeacherCount = await _analytics.CountUsersByRoleAsync("Teacher", cancellationToken),
            StudentCount = await _analytics.CountUsersByRoleAsync("Student", cancellationToken),
            SubjectCount = await _analytics.CountSubjectsAsync(cancellationToken),
            DocumentCount = await _analytics.CountDocumentsAsync(cancellationToken),
            ChatSessionCount = await _analytics.CountChatSessionsAsync(cancellationToken),
            QuestionCount = await _analytics.CountUserQuestionsAsync(cancellationToken),
            ChatByDay = chatByDay.Select(p => new ChartSeriesPointDto { Label = p.Label, Value = p.Count }).ToList(),
            UploadsByMonth = uploadsByMonth.Select(p => new ChartSeriesPointDto { Label = p.Label, Value = p.Count }).ToList(),
            TopSubjects = top.Select(s => new SubjectAccessDto
            {
                SubjectCode = s.SubjectCode,
                SubjectName = s.SubjectName,
                SessionCount = s.SessionCount
            }).ToList()
        };
    }
}
