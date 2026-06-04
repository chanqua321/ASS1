using Model.Data;

namespace Model.UnitOfWork;

public class UnitOfWork(AppDbContext db) : IUnitOfWork.IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
