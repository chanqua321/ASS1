using BusinessLogic.DTOs;

namespace BusinessLogic.IBusinessLogic;

public interface IDashboardService
{
    Task<TeacherDashboardDto> GetTeacherDashboardAsync(int teacherUserId, CancellationToken cancellationToken = default);
    Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
}
