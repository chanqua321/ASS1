using Microsoft.Extensions.Options;
using Model.Entities;
using Model.IRepository;
using BusinessLogic.DTOs;
using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;
using BusinessLogic.Options;

namespace BusinessLogic.Logic;

public class RetrievalService : IRetrievalService
{
    private readonly IChunkRepository _chunks;
    private readonly IEmbeddingService _embedding;
    private readonly RagChatOptions _options;

    public RetrievalService(
        IChunkRepository chunks,
        IEmbeddingService embedding,
        IOptions<RagChatOptions> options)
    {
        _chunks = chunks;
        _embedding = embedding;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<RetrievedChunkDto>> RetrieveAsync(
        string query,
        int? subjectId = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        // Retrieve (RAG):
        // 1) Embed query
        // 2) Load indexed chunks (theo subject nếu có)
        // 3) Cosine similarity → score
        // 4) Boost nếu query chứa metadata (mã môn/tên môn/tên file)
        // 5) Trả topK chunk
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<RetrievedChunkDto>();

        var queryVector = await _embedding.CreateEmbeddingAsync(query, cancellationToken);
        var queryLower = query.ToLowerInvariant();

        var chunks = await _chunks.GetIndexedForRetrievalAsync(subjectId, cancellationToken);

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
                    SubjectId = c.Document.SubjectId,
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
        // Summary retrieve:
        // - Nếu user nhắc đúng tên file → lấy toàn bộ chunk theo document (ordered)
        // - Nếu không → fallback sang RetrieveAsync(topK=maxChunks)
        // - Nếu vẫn rỗng → lấy fallback chunks (để vẫn có gì đó cho tóm tắt)
        var documentId = await FindDocumentIdByQuestionAsync(query, subjectId, cancellationToken);
        if (documentId.HasValue)
        {
            var allChunks = await _chunks.GetByDocumentIdOrderedAsync(documentId.Value, cancellationToken);
            return allChunks.Select(MapChunk).ToList();
        }

        var retrieved = await RetrieveAsync(query, subjectId, maxChunks, cancellationToken);
        if (retrieved.Count > 0)
            return retrieved;

        var fallback = await _chunks.GetFallbackChunksAsync(subjectId, maxChunks, cancellationToken);
        return fallback.Select(MapChunk).ToList();
    }

    private async Task<int?> FindDocumentIdByQuestionAsync(
        string question,
        int? subjectId,
        CancellationToken cancellationToken)
    {
        var q = question.ToLowerInvariant();
        var docs = await _chunks.GetIndexedDocumentNamesAsync(subjectId, cancellationToken);

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
        SubjectId = c.Document.SubjectId,
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
