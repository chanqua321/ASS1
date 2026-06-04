using Model.IRepository;
using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Entities;
using Model.Enums;

namespace Model.Repository;

public class DocumentRepository(AppDbContext db) : IDocumentRepository
{
    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        db.Documents.AnyAsync(d => d.Id == id, cancellationToken);

    public async Task AddAsync(Document document, CancellationToken cancellationToken = default) =>
        await db.Documents.AddAsync(document, cancellationToken);

    public Task<Document?> GetByIdForProcessingAsync(int id, CancellationToken cancellationToken = default) =>
        db.Documents
            .Include(d => d.Chunks)
            .Include(d => d.Subject)
            .Include(d => d.Chapter)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Document?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default) =>
        db.Documents
            .AsNoTracking()
            .Include(d => d.Subject)
            .Include(d => d.Chapter)
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Document?> GetByIdForDownloadAsync(int id, CancellationToken cancellationToken = default) =>
        db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<List<Document>> GetProcessedListAsync(
        int? subjectId,
        int? teacherUserId = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Documents
            .AsNoTracking()
            .Include(d => d.Subject)
            .Include(d => d.Chapter)
            .Include(d => d.Chunks)
            .Where(d => d.Status == DocumentStatus.Indexed ||
                        d.Status == DocumentStatus.Processing ||
                        d.Status == DocumentStatus.Failed);

        if (subjectId.HasValue)
            query = query.Where(d => d.SubjectId == subjectId);

        if (teacherUserId.HasValue)
            query = query.Where(d => d.Subject.TeacherUserId == teacherUserId);

        return query
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsForSubjectChapterAsync(
        int subjectId,
        int chapterId,
        CancellationToken cancellationToken = default) =>
        db.Documents.AnyAsync(
            d => d.SubjectId == subjectId && d.ChapterId == chapterId,
            cancellationToken);

    public Task<List<ChapterDocumentRow>> GetChapterDocumentRowsAsync(
        int subjectId,
        CancellationToken cancellationToken = default) =>
        db.Documents
            .AsNoTracking()
            .Where(d => d.SubjectId == subjectId && d.ChapterId != null)
            .Join(
                db.Chapters,
                d => d.ChapterId,
                c => c.Id,
                (d, c) => new ChapterDocumentRow { ChapterId = c.Id, ChapterTitle = c.Title })
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<string?> GetChapterTitleAsync(int chapterId, CancellationToken cancellationToken = default) =>
        db.Chapters
            .AsNoTracking()
            .Where(c => c.Id == chapterId)
            .Select(c => c.Title)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsInTeacherSubjectAsync(
        int documentId,
        int teacherUserId,
        CancellationToken cancellationToken = default) =>
        db.Documents.AnyAsync(
            d => d.Id == documentId && d.Subject.TeacherUserId == teacherUserId,
            cancellationToken);

    public Task<int?> GetUploadedByUserIdAsync(int documentId, CancellationToken cancellationToken = default) =>
        db.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => d.UploadedByUserId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Document?> GetByIdForDeleteAsync(int id, CancellationToken cancellationToken = default) =>
        db.Documents
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task DeleteCitationsForDocumentAsync(int documentId, CancellationToken cancellationToken = default) =>
        db.MessageCitations
            .Where(c => c.DocumentId == documentId)
            .ExecuteDeleteAsync(cancellationToken);

    public void Remove(Document document) => db.Documents.Remove(document);

    public Task RemoveChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        db.DocumentChunks.RemoveRange(chunks);
        return Task.CompletedTask;
    }

    public async Task AddChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default) =>
        await db.DocumentChunks.AddAsync(chunk, cancellationToken);
}
