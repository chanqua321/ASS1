using Model.IRepository;
using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Entities;
using Model.Helpers;

namespace Model.Repository;

public class SubjectRepository(AppDbContext db) : ISubjectRepository
{
    public Task<List<Subject>> GetAllWithChaptersAsync(CancellationToken cancellationToken = default) =>
        db.Subjects
            .Include(s => s.TeacherUser)
            .Include(s => s.Chapters.OrderBy(c => c.OrderNumber))
            .OrderBy(s => s.Code)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<Subject?> GetByIdWithChaptersAsync(int id, CancellationToken cancellationToken = default) =>
        db.Subjects
            .Include(s => s.Chapters)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Subject?> FindByNameAsync(string name, CancellationToken cancellationToken = default) =>
        db.Subjects.FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower(), cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        db.Subjects.AnyAsync(s => s.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        db.Subjects.AnyAsync(s => s.Code == code, cancellationToken);

    public async Task AddAsync(Subject subject, CancellationToken cancellationToken = default) =>
        await db.Subjects.AddAsync(subject, cancellationToken);

    public Task<int?> GetMaxChapterOrderAsync(int subjectId, CancellationToken cancellationToken = default) =>
        db.Chapters
            .Where(c => c.SubjectId == subjectId)
            .Select(c => (int?)c.OrderNumber)
            .MaxAsync(cancellationToken);

    public async Task AddChapterAsync(Chapter chapter, CancellationToken cancellationToken = default) =>
        await db.Chapters.AddAsync(chapter, cancellationToken);

    public Task<bool> ChapterBelongsToSubjectAsync(int chapterId, int subjectId, CancellationToken cancellationToken = default) =>
        db.Chapters.AnyAsync(c => c.Id == chapterId && c.SubjectId == subjectId, cancellationToken);

    public async Task<Chapter?> FindChapterBySimilarTitleAsync(
        int subjectId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var chapters = await db.Chapters
            .AsNoTracking()
            .Where(c => c.SubjectId == subjectId)
            .ToListAsync(cancellationToken);

        return chapters.FirstOrDefault(c => ChapterTitleHelper.AreSimilar(c.Title, title));
    }

    public Task<List<Subject>> GetAllWithTeacherAsync(CancellationToken cancellationToken = default) =>
        db.Subjects
            .Include(s => s.TeacherUser)
            .OrderBy(s => s.Code)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<Subject?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        db.Subjects.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
}
