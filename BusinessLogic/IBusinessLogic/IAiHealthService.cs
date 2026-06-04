using BusinessLogic.DTOs;

namespace BusinessLogic.IBusinessLogic;

public interface IAiHealthService
{
    Task<AiStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
}
