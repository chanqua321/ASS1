using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessLogic.IBusinessLogic;

namespace Web.Controllers;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> Teacher(CancellationToken cancellationToken)
    {
        var data = await _dashboard.GetTeacherDashboardAsync(GetCurrentUserId(), cancellationToken);
        return View(data);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Admin(CancellationToken cancellationToken)
    {
        var data = await _dashboard.GetAdminDashboardAsync(cancellationToken);
        return View(data);
    }

    private int GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(raw, out var id))
            throw new InvalidOperationException("Không xác định được tài khoản đăng nhập.");
        return id;
    }
}
