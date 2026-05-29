using Service.DTOs;

namespace Service.Interfaces;

public interface IAiHealthService
{
    Task<AiStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
}
