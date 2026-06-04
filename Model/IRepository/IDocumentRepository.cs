using Model.Entities;
using Model.Enums;

namespace Model.IRepository;

public sealed class ChapterDocumentRow
{
    public int ChapterId { get; init; }
    public string ChapterTitle { get; init; } = string.Empty;
}

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
    Task<List<ChapterDocumentRow>> GetChapterDocumentRowsAsync(
        int subjectId,
        CancellationToken cancellationToken = default);
    Task<string?> GetChapterTitleAsync(int chapterId, CancellationToken cancellationToken = default);
    Task<bool> IsInTeacherSubjectAsync(
        int documentId,
        int teacherUserId,
        CancellationToken cancellationToken = default);
    Task<int?> GetUploadedByUserIdAsync(int documentId, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdForDeleteAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteCitationsForDocumentAsync(int documentId, CancellationToken cancellationToken = default);
    void Remove(Document document);
    Task RemoveChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task AddChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);
}
