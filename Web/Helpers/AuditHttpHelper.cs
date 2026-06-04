using System.Security.Claims;
using BusinessLogic.IBusinessLogic;

namespace Web.Helpers;

public static class AuditHttpHelper
{
    public static async Task LogAsync(
        HttpContext http,
        IAuditService audit,
        string action,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        var user = http.User;
        int? userId = null;
        var idRaw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idRaw, out var id))
            userId = id;

        var username = user.Identity?.IsAuthenticated == true
            ? user.Identity.Name ?? "anonymous"
            : "anonymous";

        var ip = http.Connection.RemoteIpAddress?.ToString();
        await audit.LogAsync(userId, username, action, ip, detail, cancellationToken);
    }
}
