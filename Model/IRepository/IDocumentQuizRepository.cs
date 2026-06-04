using Model.Entities;

namespace Model.IRepository;

public interface IDocumentQuizRepository
{
    Task<DocumentQuiz?> GetLatestByDocumentIdAsync(int documentId, CancellationToken cancellationToken = default);
    Task<List<DocumentQuiz>> GetHistoryByDocumentIdAsync(int documentId, int take, CancellationToken cancellationToken = default);
    Task<DocumentQuiz?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(DocumentQuiz quiz, CancellationToken cancellationToken = default);
}
