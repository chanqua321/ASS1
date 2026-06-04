using Model.IRepository;
using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Entities;
using Model.Enums;

namespace Model.Repository;

public class ChunkRepository(AppDbContext db) : IChunkRepository
{
    public Task<List<DocumentChunk>> GetIndexedForRetrievalAsync(int? subjectId, CancellationToken cancellationToken = default)
    {
        var query = db.DocumentChunks
            .AsNoTracking()
            .Include(c => c.Embedding)
            .Include(c => c.Document)
                .ThenInclude(d => d.Subject)
            .Include(c => c.Document)
                .ThenInclude(d => d.Chapter)
            .Where(c => c.Document.Status == DocumentStatus.Indexed && c.Embedding != null);

        if (subjectId.HasValue)
            query = query.Where(c => c.Document.SubjectId == subjectId);

        return query.ToListAsync(cancellationToken);
    }

    public Task<List<DocumentChunk>> GetByDocumentIdOrderedAsync(int documentId, CancellationToken cancellationToken = default) =>
        db.DocumentChunks
            .AsNoTracking()
            .Include(c => c.Document)
                .ThenInclude(d => d.Subject)
            .Include(c => c.Document)
                .ThenInclude(d => d.Chapter)
            .Where(c => c.DocumentId == documentId && c.Document.Status == DocumentStatus.Indexed)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken);

    public Task<List<DocumentChunk>> GetFallbackChunksAsync(int? subjectId, int take, CancellationToken cancellationToken = default) =>
        db.DocumentChunks
            .AsNoTracking()
            .Include(c => c.Document)
                .ThenInclude(d => d.Subject)
            .Include(c => c.Document)
                .ThenInclude(d => d.Chapter)
            .Where(c => c.Document.Status == DocumentStatus.Indexed &&
                        (!subjectId.HasValue || c.Document.SubjectId == subjectId))
            .OrderByDescending(c => c.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<List<(int Id, string FileName)>> GetIndexedDocumentNamesAsync(
        int? subjectId,
        CancellationToken cancellationToken = default)
    {
        var docs = await db.Documents
            .AsNoTracking()
            .Where(d => d.Status == DocumentStatus.Indexed &&
                        (!subjectId.HasValue || d.SubjectId == subjectId))
            .Select(d => new { d.Id, d.FileName })
            .ToListAsync(cancellationToken);

        return docs.Select(d => (d.Id, d.FileName)).ToList();
    }
}
