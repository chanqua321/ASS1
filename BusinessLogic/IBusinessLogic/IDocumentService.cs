using BusinessLogic.DTOs;

namespace BusinessLogic.IBusinessLogic;

public interface IDocumentService
{
    Task<DocumentListItemDto> UploadAsync(DocumentUploadRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentListItemDto>> GetProcessedDocumentsAsync(
        int? subjectId = null,
        int? teacherUserId = null,
        int? viewerUserId = null,
        bool viewerIsAdmin = false,
        CancellationToken cancellationToken = default);
    Task<bool> TeacherCanAccessAsync(int documentId, int teacherUserId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<DocumentListItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DocumentDetailsDto?> GetDetailsAsync(
        int id,
        int? viewerUserId = null,
        bool viewerIsAdmin = false,
        CancellationToken cancellationToken = default);
    Task<DocumentDownloadDto?> GetDownloadAsync(int id, CancellationToken cancellationToken = default);
    Task ProcessDocumentAsync(int documentId, CancellationToken cancellationToken = default);
    Task<(bool Success, string ErrorMessage)> DeleteAsync(
        int documentId,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
    Task<bool> CanDeleteAsync(int documentId, int userId, bool isAdmin, CancellationToken cancellationToken = default);
}
