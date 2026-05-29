using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Model.Data;
using Model.Entities;
using Model.Enums;
using Service.DTOs;
using Service.Helpers;
using Service.Interfaces;
using Service.Options;

namespace Service.Implementations;

public class RetrievalService : IRetrievalService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embedding;
    private readonly RagChatOptions _options;

    public RetrievalService(
        AppDbContext db,
        IEmbeddingService embedding,
        IOptions<RagChatOptions> options)
    {
        _db = db;
        _embedding = embedding;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<RetrievedChunkDto>> RetrieveAsync(
        string query,
        int? subjectId = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<RetrievedChunkDto>();

        var queryVector = await _embedding.CreateEmbeddingAsync(query, cancellationToken);
        var queryLower = query.ToLowerInvariant();

        var chunkQuery = _db.DocumentChunks
            .AsNoTracking()
            .Include(c => c.Embedding)
            .Include(c => c.Document)
                .ThenInclude(d => d.Subject)
            .Include(c => c.Document)
                .ThenInclude(d => d.Chapter)
            .Where(c => c.Document.Status == DocumentStatus.Indexed && c.Embedding != null);

        if (subjectId.HasValue)
            chunkQuery = chunkQuery.Where(c => c.Document.SubjectId == subjectId);

        var chunks = await chunkQuery.ToListAsync(cancellationToken);

        var scored = chunks
            .Select(c =>
            {
                var vector = VectorHelper.ParseVector(c.Embedding!.VectorJson);
                var score = VectorHelper.CosineSimilarity(queryVector, vector);
                return new RetrievedChunkDto
                {
                    ChunkId = c.Id,
                    DocumentId = c.DocumentId,
                    FileName = c.Document.FileName,
                    ChapterTitle = c.Document.Chapter?.Title,
                    SubjectName = c.Document.Subject.Name,
                    SubjectCode = c.Document.Subject.Code,
                    Content = c.Content,
                    Score = score
                };
            })
            .ToList();

        ApplyMetadataBoost(scored, queryLower);

        return scored
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
    }

    public async Task<IReadOnlyList<RetrievedChunkDto>> RetrieveForSummaryAsync(
        string query,
        int? subjectId = null,
        int maxChunks = 12,
        CancellationToken cancellationToken = default)
    {
        var documentId = await FindDocumentIdByQuestionAsync(query, subjectId, cancellationToken);
        if (documentId.HasValue)
        {
            var allChunks = await _db.DocumentChunks
                .AsNoTracking()
                .Include(c => c.Document)
                    .ThenInclude(d => d.Subject)
                .Include(c => c.Document)
                    .ThenInclude(d => d.Chapter)
                .Where(c => c.DocumentId == documentId.Value &&
                            c.Document.Status == DocumentStatus.Indexed)
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync(cancellationToken);

            return allChunks.Select(MapChunk).ToList();
        }

        var retrieved = await RetrieveAsync(query, subjectId, maxChunks, cancellationToken);
        if (retrieved.Count > 0)
            return retrieved;

        var fallback = await _db.DocumentChunks
            .AsNoTracking()
            .Include(c => c.Document)
                .ThenInclude(d => d.Subject)
            .Include(c => c.Document)
                .ThenInclude(d => d.Chapter)
            .Where(c => c.Document.Status == DocumentStatus.Indexed &&
                        (!subjectId.HasValue || c.Document.SubjectId == subjectId))
            .OrderByDescending(c => c.Id)
            .Take(maxChunks)
            .ToListAsync(cancellationToken);

        return fallback.Select(MapChunk).ToList();
    }

    private async Task<int?> FindDocumentIdByQuestionAsync(
        string question,
        int? subjectId,
        CancellationToken cancellationToken)
    {
        var q = question.ToLowerInvariant();
        var docs = await _db.Documents
            .AsNoTracking()
            .Where(d => d.Status == DocumentStatus.Indexed &&
                        (!subjectId.HasValue || d.SubjectId == subjectId))
            .Select(d => new { d.Id, d.FileName })
            .ToListAsync(cancellationToken);

        foreach (var doc in docs.OrderByDescending(d => d.FileName.Length))
        {
            var name = doc.FileName.ToLowerInvariant();
            var baseName = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
            if (q.Contains(name) || (!string.IsNullOrEmpty(baseName) && q.Contains(baseName)))
                return doc.Id;
        }

        return null;
    }

    private static RetrievedChunkDto MapChunk(DocumentChunk c) => new()
    {
        ChunkId = c.Id,
        DocumentId = c.DocumentId,
        FileName = c.Document.FileName,
        ChapterTitle = c.Document.Chapter?.Title,
        SubjectName = c.Document.Subject.Name,
        SubjectCode = c.Document.Subject.Code,
        Content = c.Content,
        Score = 1.0
    };

    private void ApplyMetadataBoost(List<RetrievedChunkDto> scored, string queryLower)
    {
        foreach (var item in scored)
        {
            var code = item.SubjectCode.ToLowerInvariant();
            var name = item.SubjectName.ToLowerInvariant();
            var file = item.FileName.ToLowerInvariant();

            if ((!string.IsNullOrEmpty(code) && queryLower.Contains(code)) ||
                (!string.IsNullOrEmpty(name) && queryLower.Contains(name)) ||
                queryLower.Contains(file))
            {
                item.Score = Math.Max(item.Score, _options.MetadataBoostScore);
            }
        }
    }
}
