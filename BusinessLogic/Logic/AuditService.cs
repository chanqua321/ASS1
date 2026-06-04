using BusinessLogic.DTOs;
using BusinessLogic.IBusinessLogic;
using Model.Entities;
using Model.IRepository;
using Model.IUnitOfWork;

namespace BusinessLogic.Logic;

public class AuditService : IAuditService
{
    private readonly IAuditRepository _audit;
    private readonly IUnitOfWork _uow;

    public AuditService(IAuditRepository audit, IUnitOfWork uow)
    {
        _audit = audit;
        _uow = uow;
    }

    public async Task LogAsync(
        int? userId,
        string username,
        string action,
        string? ipAddress,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        await _audit.AddAsync(new AuditLog
        {
            UserId = userId,
            Username = (username ?? string.Empty).Trim(),
            Action = action,
            IpAddress = ipAddress,
            Detail = detail?.Length > 1000 ? detail[..1000] : detail,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default)
    {
        var items = await _audit.GetRecentAsync(take, cancellationToken);
        return items.Select(x => new AuditLogDto
        {
            Id = x.Id,
            UserId = x.UserId,
            Username = x.Username,
            Action = x.Action,
            CreatedAt = x.CreatedAt,
            IpAddress = x.IpAddress,
            Detail = x.Detail
        }).ToList();
    }
}
