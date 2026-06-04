using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessLogic.IBusinessLogic;

namespace Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminAuditController : Controller
{
    private readonly IAuditService _audit;

    public AdminAuditController(IAuditService audit)
    {
        _audit = audit;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var logs = await _audit.GetRecentAsync(300, cancellationToken);
        return View(logs);
    }
}
