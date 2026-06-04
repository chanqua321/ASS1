using Model.Entities;

namespace Model.IRepository;

public interface IUserRepository
{
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AppUser?> FindByEmailIgnoreCaseAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(AppUser user, CancellationToken cancellationToken = default);
    void Remove(AppUser user);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

