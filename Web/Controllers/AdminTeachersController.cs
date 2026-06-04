using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Web.ViewModels;
using Web.Helpers;
using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;

namespace Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminTeachersController : Controller
{
    private readonly ITeacherAssignmentService _teacherAssignments;
    private readonly IAuditService _audit;

    public AdminTeachersController(ITeacherAssignmentService teacherAssignments, IAuditService audit)
    {
        _teacherAssignments = teacherAssignments;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? subjectId, CancellationToken cancellationToken)
    {
        return View(await BuildPageAsync(subjectId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AdminTeachersIndexViewModel model, CancellationToken cancellationToken)
    {
        model.Email = (model.Email ?? string.Empty).Trim();

        if (!model.SubjectId.HasValue)
            ModelState.AddModelError(nameof(model.SubjectId), "Chọn môn chưa có giáo viên.");

        var hints = await _teacherAssignments.GetFormValidationHintsAsync(model.Email, cancellationToken);
        if (hints.RequiresPassword && string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Nhập mật khẩu cho tài khoản mới hoặc Student.");

        if (!ModelState.IsValid)
            return View(await BuildPageAsync(model.SubjectId, cancellationToken, model));

        try
        {
            var result = await _teacherAssignments.AssignTeacherAsync(
                model.Email,
                model.Password,
                model.SubjectId!.Value,
                cancellationToken);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage);
                return View(await BuildPageAsync(model.SubjectId, cancellationToken, model));
            }

            await AuditHttpHelper.LogAsync(
                HttpContext, _audit, AuditActions.AssignTeacher,
                $"Subject={result.SubjectCode}; Teacher={result.TeacherEmail}",
                cancellationToken);

            if (result.CreatedTeacher || result.PromotedFromStudent)
            {
                await AuditHttpHelper.LogAsync(
                    HttpContext, _audit, AuditActions.CreateTeacher,
                    $"Email={result.TeacherEmail}",
                    cancellationToken);
            }

            TempData["Success"] = $"Đã gán {result.TeacherEmail} phụ trách môn {result.SubjectCode}.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Lỗi hệ thống: {ex.Message}");
            return View(await BuildPageAsync(model.SubjectId, cancellationToken, model));
        }
    }

    private async Task<AdminTeachersIndexViewModel> BuildPageAsync(
        int? preselectSubjectId,
        CancellationToken cancellationToken,
        AdminTeachersIndexViewModel? form = null)
    {
        var page = await _teacherAssignments.GetAdminPageAsync(preselectSubjectId, cancellationToken);

        if (page.PreselectedSubjectAlreadyAssigned)
            TempData["Error"] = "Môn này đã có giáo viên.";

        return new AdminTeachersIndexViewModel
        {
            Email = form?.Email ?? string.Empty,
            Password = string.Empty,
            SubjectId = form?.SubjectId ?? preselectSubjectId,
            AvailableSubjects = page.AvailableSubjects.Select(s => new SelectListItem
            {
                Text = $"{s.Code} — {s.Name}",
                Value = s.Id.ToString(),
                Selected = (form?.SubjectId ?? preselectSubjectId) == s.Id
            }).ToList(),
            Teachers = page.Teachers.Select(t => new TeacherAssignmentRow
            {
                Email = t.Email,
                Subjects = t.SubjectLabels
            }).ToList(),
            UnassignedSubjects = page.UnassignedSubjects.Select(s => new UnassignedSubjectRow
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name
            }).ToList()
        };
    }
}
