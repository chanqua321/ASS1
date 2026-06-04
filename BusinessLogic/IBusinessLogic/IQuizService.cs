using BusinessLogic.DTOs;

namespace BusinessLogic.IBusinessLogic;

public interface IQuizService
{
    Task<(bool Success, string Error, DocumentQuizDto? Quiz)> GenerateAsync(
        int documentId,
        int userId,
        int questionCount,
        CancellationToken cancellationToken = default);

    Task<DocumentQuizDto?> GetLatestAsync(int documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentQuizDto>> GetHistoryAsync(int documentId, int take = 10, CancellationToken cancellationToken = default);
    Task<DocumentQuizDto?> GetByIdAsync(int quizId, CancellationToken cancellationToken = default);
    string BuildExportContent(DocumentQuizDto quiz, string format);
}
