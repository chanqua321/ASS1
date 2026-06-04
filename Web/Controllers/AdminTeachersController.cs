using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Model.Data;
using Web.ViewModels;
using Web.Helpers;
using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;

namespace Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminTeachersController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAuthService _auth;
    private readonly IAuditService _audit;

    public AdminTeachersController(AppDbContext db, IAuthService auth, IAuditService audit)
    {
        _db = db;
        _auth = auth;
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

        var existingUser = await _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == model.Email, cancellationToken);
        var isExistingTeacher = existingUser?.Role == "Teacher";
        if (!isExistingTeacher && string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Nhập mật khẩu cho tài khoản mới hoặc Student.");

        if (!ModelState.IsValid)
            return View(await BuildPageAsync(model.SubjectId, cancellationToken, model));

        var subjectEntity = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == model.SubjectId!.Value, cancellationToken);
        if (subjectEntity is null)
        {
            ModelState.AddModelError(nameof(model.SubjectId), "Môn học không tồn tại.");
            return View(await BuildPageAsync(model.SubjectId, cancellationToken, model));
        }

        if (subjectEntity.TeacherUserId.HasValue)
        {
            ModelState.AddModelError(string.Empty, $"Môn {subjectEntity.Code} đã có giáo viên.");
            return View(await BuildPageAsync(model.SubjectId, cancellationToken, model));
        }

        try
        {
            var existingBefore = existingUser;
            var (ok, err, user) = await _auth.PrepareTeacherUserAsync(model.Email, model.Password, cancellationToken);
            if (!ok || user is null)
            {
                ModelState.AddModelError(string.Empty, err);
                return View(await BuildPageAsync(model.SubjectId, cancellationToken, model));
            }

            subjectEntity.TeacherUserId = user.Id;
            await _db.SaveChangesAsync(cancellationToken);

            await AuditHttpHelper.LogAsync(
                HttpContext, _audit, AuditActions.AssignTeacher,
                $"Subject={subjectEntity.Code}; Teacher={user.Email}",
                cancellationToken);
            if (existingBefore is null || existingBefore.Role == "Student")
            {
                await AuditHttpHelper.LogAsync(
                    HttpContext, _audit, AuditActions.CreateTeacher,
                    $"Email={user.Email}",
                    cancellationToken);
            }

            TempData["Success"] = $"Đã gán {user.Email} phụ trách môn {subjectEntity.Code}.";
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
        var subjects = await _db.Subjects.AsNoTracking().OrderBy(s => s.Code).ToListAsync(cancellationToken);
        var available = subjects.Where(s => !s.TeacherUserId.HasValue).ToList();

        var assigned = await _db.Subjects
            .AsNoTracking()
            .Where(s => s.TeacherUserId != null)
            .Join(
                _db.AppUsers.AsNoTracking(),
                s => s.TeacherUserId,
                u => u.Id,
                (s, u) => new { u.Email, Label = $"{s.Code} — {s.Name}" })
            .ToListAsync(cancellationToken);

        var teachers = assigned
            .GroupBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g => new TeacherAssignmentRow
            {
                Email = g.Key,
                Subjects = g.Select(x => x.Label).OrderBy(x => x).ToList()
            })
            .ToList();

        var vm = new AdminTeachersIndexViewModel
        {
            Email = form?.Email ?? string.Empty,
            Password = string.Empty,
            SubjectId = form?.SubjectId ?? preselectSubjectId,
            AvailableSubjects = available.Select(s => new SelectListItem
            {
                Text = $"{s.Code} — {s.Name}",
                Value = s.Id.ToString(),
                Selected = (form?.SubjectId ?? preselectSubjectId) == s.Id
            }).ToList(),
            Teachers = teachers,
            UnassignedSubjects = available.Select(s => new UnassignedSubjectRow
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name
            }).ToList()
        };

        if (preselectSubjectId is > 0 && subjects.FirstOrDefault(s => s.Id == preselectSubjectId)?.TeacherUserId is not null)
            TempData["Error"] = "Môn này đã có giáo viên.";

        return vm;
    }
}
