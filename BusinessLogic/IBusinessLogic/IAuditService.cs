using BusinessLogic.DTOs;

namespace BusinessLogic.IBusinessLogic;

public interface IAuditService
{
    Task LogAsync(
        int? userId,
        string username,
        string action,
        string? ipAddress,
        string? detail = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default);
}
