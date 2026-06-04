using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Entities;
using Model.IRepository;

namespace Model.Repository;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        db.AppUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<AppUser?> FindByEmailIgnoreCaseAsync(string email, CancellationToken cancellationToken = default) =>
        db.AppUsers.FirstOrDefaultAsync(
            u => u.Email.ToLower() == email.ToLower(),
            cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        db.AppUsers.AnyAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

    public async Task AddAsync(AppUser user, CancellationToken cancellationToken = default) =>
        await db.AppUsers.AddAsync(user, cancellationToken);

    public void Remove(AppUser user) => db.AppUsers.Remove(user);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        db.AppUsers.CountAsync(cancellationToken);
}

