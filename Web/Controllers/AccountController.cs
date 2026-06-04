using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.ViewModels;
using Web.Helpers;
using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;

namespace Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _auth;
    private readonly IAuditService _audit;

    public AccountController(IAuthService auth, IAuditService audit)
    {
        _auth = auth;
        _audit = audit;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (ok, error, userId, username, role) = await _auth.ValidateLoginAsync(
            model.Email, model.Password, cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                RedirectUri = model.ReturnUrl
            });

        await AuditHttpHelper.LogAsync(HttpContext, _audit, AuditActions.Login, cancellationToken: cancellationToken);

        var returnUrl = model.ReturnUrl;
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Documents");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (ok, error) = await _auth.RegisterStudentAsync(model.Email, model.Password, cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        // Đăng ký xong → chuyển sang login (hoặc tự login nếu muốn)
        return RedirectToAction(nameof(Login), new { returnUrl = model.ReturnUrl });
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => RedirectToAction(nameof(Login));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await AuditHttpHelper.LogAsync(HttpContext, _audit, AuditActions.Logout, cancellationToken: cancellationToken);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}

