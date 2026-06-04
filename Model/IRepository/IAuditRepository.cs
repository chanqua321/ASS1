using Model.Entities;

namespace Model.IRepository;

public interface IAuditRepository
{
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);
    Task<List<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
