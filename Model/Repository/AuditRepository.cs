using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Entities;
using Model.IRepository;

namespace Model.Repository;

public class AuditRepository(AppDbContext db) : IAuditRepository
{
    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default) =>
        await db.AuditLogs.AddAsync(log, cancellationToken);

    public Task<List<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken = default) =>
        db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
}
