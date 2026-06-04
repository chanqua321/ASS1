using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

/// <summary>Chuyển hướng URL cũ sang AdminTeachers.</summary>
[Authorize(Roles = "Admin")]
public class AdminUsersController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "AdminTeachers");

    public IActionResult CreateTeacher(int? subjectId) =>
        RedirectToAction("Index", "AdminTeachers", new { subjectId });
}
