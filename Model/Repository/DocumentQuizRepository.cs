using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Entities;
using Model.IRepository;

namespace Model.Repository;

public class DocumentQuizRepository(AppDbContext db) : IDocumentQuizRepository
{
    public Task<DocumentQuiz?> GetLatestByDocumentIdAsync(int documentId, CancellationToken cancellationToken = default) =>
        db.DocumentQuizzes
            .AsNoTracking()
            .Include(q => q.CreatedByUser)
            .Where(q => q.DocumentId == documentId)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<DocumentQuiz>> GetHistoryByDocumentIdAsync(int documentId, int take, CancellationToken cancellationToken = default) =>
        db.DocumentQuizzes
            .AsNoTracking()
            .Include(q => q.CreatedByUser)
            .Where(q => q.DocumentId == documentId)
            .OrderByDescending(q => q.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<DocumentQuiz?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.DocumentQuizzes
            .AsNoTracking()
            .Include(q => q.Document)
            .Include(q => q.CreatedByUser)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task AddAsync(DocumentQuiz quiz, CancellationToken cancellationToken = default) =>
        await db.DocumentQuizzes.AddAsync(quiz, cancellationToken);
}
