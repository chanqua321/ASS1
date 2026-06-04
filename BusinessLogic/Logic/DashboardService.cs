using BusinessLogic.DTOs;
using BusinessLogic.IBusinessLogic;
using Model.IRepository;

namespace BusinessLogic.Logic;

public class DashboardService : IDashboardService
{
    private readonly IAnalyticsRepository _analytics;

    public DashboardService(IAnalyticsRepository analytics)
    {
        _analytics = analytics;
    }

    public async Task<TeacherDashboardDto> GetTeacherDashboardAsync(
        int teacherUserId,
        CancellationToken cancellationToken = default)
    {
        var recent = await _analytics.GetRecentDocumentsForTeacherAsync(teacherUserId, 5, cancellationToken);

        return new TeacherDashboardDto
        {
            SubjectCount = await _analytics.CountSubjectsForTeacherAsync(teacherUserId, cancellationToken),
            DocumentCount = await _analytics.CountDocumentsForTeacherAsync(teacherUserId, cancellationToken),
            ChunkCount = await _analytics.CountChunksForTeacherAsync(teacherUserId, cancellationToken),
            StudentChatMessageCount = await _analytics.CountStudentChatMessagesForTeacherAsync(teacherUserId, cancellationToken),
            RecentDocuments = recent.Select(r => new TeacherRecentDocumentDto
            {
                Id = r.Id,
                FileName = r.FileName,
                SubjectName = r.SubjectName,
                ChapterTitle = r.ChapterTitle,
                UploadedAt = r.UploadedAt,
                SummaryPreview = r.SummaryPreview
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
