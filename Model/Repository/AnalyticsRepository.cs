using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Enums;
using Model.IRepository;

namespace Model.Repository;

public class AnalyticsRepository(AppDbContext db) : IAnalyticsRepository
{
    public Task<int> CountSubjectsForTeacherAsync(int teacherUserId, CancellationToken cancellationToken = default) =>
        db.Subjects.CountAsync(s => s.TeacherUserId == teacherUserId, cancellationToken);

    public Task<int> CountDocumentsForTeacherAsync(int teacherUserId, CancellationToken cancellationToken = default) =>
        db.Documents.CountAsync(
            d => d.Subject.TeacherUserId == teacherUserId,
            cancellationToken);

    public Task<int> CountChunksForTeacherAsync(int teacherUserId, CancellationToken cancellationToken = default) =>
        db.DocumentChunks.CountAsync(
            c => c.Document.Subject.TeacherUserId == teacherUserId,
            cancellationToken);

    public Task<int> CountStudentChatMessagesForTeacherAsync(int teacherUserId, CancellationToken cancellationToken = default) =>
        db.ChatMessages.CountAsync(
            m => m.Role == ChatMessageRole.User &&
                 (
                     (m.Session.SubjectId != null &&
                      m.Session.Subject!.TeacherUserId == teacherUserId)
                     || m.Citations.Any(c =>
                         db.Documents.Any(d =>
                             d.Id == c.DocumentId && d.Subject.TeacherUserId == teacherUserId))
                     || m.Session.Messages.Any(x =>
                         x.Role == ChatMessageRole.Assistant &&
                         x.Citations.Any(c =>
                             db.Documents.Any(d =>
                                 d.Id == c.DocumentId && d.Subject.TeacherUserId == teacherUserId)))
                 ),
            cancellationToken);

    public async Task<List<TeacherRecentDocumentRow>> GetRecentDocumentsForTeacherAsync(
        int teacherUserId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var docs = await db.Documents
            .AsNoTracking()
            .Include(d => d.Subject)
            .Include(d => d.Chapter)
            .Where(d => d.Subject.TeacherUserId == teacherUserId)
            .OrderByDescending(d => d.UploadedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return docs.Select(d => new TeacherRecentDocumentRow
        {
            Id = d.Id,
            FileName = d.FileName,
            SubjectName = d.Subject.Name,
            ChapterTitle = d.Chapter?.Title,
            UploadedAt = d.UploadedAt,
            SummaryPreview = BuildPreview(d.Summary)
        }).ToList();
    }

    public Task<int> CountUsersAsync(CancellationToken cancellationToken = default) =>
        db.AppUsers.CountAsync(cancellationToken);

    public Task<int> CountUsersByRoleAsync(string role, CancellationToken cancellationToken = default) =>
        db.AppUsers.CountAsync(u => u.Role == role, cancellationToken);

    public Task<int> CountSubjectsAsync(CancellationToken cancellationToken = default) =>
        db.Subjects.CountAsync(cancellationToken);

    public Task<int> CountDocumentsAsync(CancellationToken cancellationToken = default) =>
        db.Documents.CountAsync(cancellationToken);

    public Task<int> CountChatSessionsAsync(CancellationToken cancellationToken = default) =>
        db.ChatSessions.CountAsync(cancellationToken);

    public Task<int> CountUserQuestionsAsync(CancellationToken cancellationToken = default) =>
        db.ChatMessages.CountAsync(m => m.Role == ChatMessageRole.User, cancellationToken);

    public async Task<List<ChartCountRow>> GetChatCountByDayAsync(int days, CancellationToken cancellationToken = default)
    {
        var from = DateTime.UtcNow.Date.AddDays(-(days - 1));
        var raw = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.Role == ChatMessageRole.User && m.CreatedAt >= from)
            .GroupBy(m => new { m.CreatedAt.Year, m.CreatedAt.Month, m.CreatedAt.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var map = raw.ToDictionary(
            x => new DateTime(x.Year, x.Month, x.Day),
            x => x.Count);

        var result = new List<ChartCountRow>();
        for (var i = 0; i < days; i++)
        {
            var day = from.AddDays(i);
            map.TryGetValue(day, out var count);
            result.Add(new ChartCountRow
            {
                Label = day.ToString("dd/MM"),
                Count = count
            });
        }

        return result;
    }

    public async Task<List<ChartCountRow>> GetDocumentUploadCountByMonthAsync(int months, CancellationToken cancellationToken = default)
    {
        var firstMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-(months - 1));
        var raw = await db.Documents
            .AsNoTracking()
            .Where(d => d.UploadedAt >= firstMonth)
            .GroupBy(d => new { d.UploadedAt.Year, d.UploadedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var map = raw.ToDictionary(
            x => new DateTime(x.Year, x.Month, 1),
            x => x.Count);

        var result = new List<ChartCountRow>();
        for (var i = 0; i < months; i++)
        {
            var month = firstMonth.AddMonths(i);
            map.TryGetValue(month, out var count);
            result.Add(new ChartCountRow
            {
                Label = month.ToString("MM/yyyy"),
                Count = count
            });
        }

        return result;
    }

    public async Task<List<SubjectAccessRow>> GetTopSubjectsByChatSessionsAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        // Phiên có môn chọn sẵn (dropdown) hoặc đã gán sau RAG.
        var explicitCounts = await db.ChatSessions
            .AsNoTracking()
            .Where(s => s.SubjectId != null)
            .GroupBy(s => s.SubjectId!.Value)
            .Select(g => new { SubjectId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Phiên "Tất cả tài liệu": suy môn từ citation tài liệu trong hội thoại.
        var inferredCounts = await (
                from c in db.MessageCitations.AsNoTracking()
                join m in db.ChatMessages on c.MessageId equals m.Id
                join s in db.ChatSessions on m.SessionId equals s.Id
                join d in db.Documents on c.DocumentId equals d.Id
                where s.SubjectId == null
                select new { SessionId = s.Id, d.SubjectId })
            .Distinct()
            .GroupBy(x => x.SubjectId)
            .Select(g => new { SubjectId = g.Key, Count = g.Select(x => x.SessionId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        var merged = new Dictionary<int, int>();
        foreach (var row in explicitCounts)
            merged[row.SubjectId] = merged.GetValueOrDefault(row.SubjectId) + row.Count;
        foreach (var row in inferredCounts)
            merged[row.SubjectId] = merged.GetValueOrDefault(row.SubjectId) + row.Count;

        if (merged.Count == 0)
            return new List<SubjectAccessRow>();

        var top = merged
            .OrderByDescending(kv => kv.Value)
            .Take(take)
            .ToList();

        var ids = top.Select(kv => kv.Key).ToList();
        var subjects = await db.Subjects
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        return top
            .Where(kv => subjects.ContainsKey(kv.Key))
            .Select(kv =>
            {
                var s = subjects[kv.Key];
                return new SubjectAccessRow
                {
                    SubjectCode = s.Code,
                    SubjectName = s.Name,
                    SessionCount = kv.Value
                };
            })
            .ToList();
    }

    private static string? BuildPreview(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return null;
        const int max = 120;
        var t = summary.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return t.Length <= max ? t : t[..max] + "…";
    }
}
