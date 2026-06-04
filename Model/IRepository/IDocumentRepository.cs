using Model.Entities;
using Model.Enums;

namespace Model.IRepository;

public interface IDocumentRepository
{
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Document document, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdForProcessingAsync(int id, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdForDownloadAsync(int id, CancellationToken cancellationToken = default);
    Task<List<Document>> GetProcessedListAsync(
        int? subjectId,
        int? teacherUserId = null,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsForSubjectChapterAsync(
        int subjectId,
        int chapterId,
        CancellationToken cancellationToken = default);
    Task<bool> IsInTeacherSubjectAsync(
        int documentId,
        int teacherUserId,
        CancellationToken cancellationToken = default);
    Task RemoveChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task AddChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);
}
