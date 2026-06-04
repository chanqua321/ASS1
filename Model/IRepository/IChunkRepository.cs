using Model.Entities;

namespace Model.IRepository;

public interface IChunkRepository
{
    Task<List<DocumentChunk>> GetIndexedForRetrievalAsync(int? subjectId, CancellationToken cancellationToken = default);
    Task<List<DocumentChunk>> GetByDocumentIdOrderedAsync(int documentId, CancellationToken cancellationToken = default);
    Task<List<DocumentChunk>> GetFallbackChunksAsync(int? subjectId, int take, CancellationToken cancellationToken = default);
    Task<List<(int Id, string FileName)>> GetIndexedDocumentNamesAsync(int? subjectId, CancellationToken cancellationToken = default);
}
