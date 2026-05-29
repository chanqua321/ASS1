using Service.DTOs;

namespace Service.Interfaces;

public interface IDocumentService
{
    Task<DocumentListItemDto> UploadAsync(DocumentUploadRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentListItemDto>> GetProcessedDocumentsAsync(int? subjectId = null, CancellationToken cancellationToken = default);
    Task<DocumentListItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DocumentDownloadDto?> GetDownloadAsync(int id, CancellationToken cancellationToken = default);
    Task ProcessDocumentAsync(int documentId, CancellationToken cancellationToken = default);
}
