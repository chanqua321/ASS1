using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessLogic.IBusinessLogic;
using Web.ViewModels;

namespace Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminSubjectsController : Controller
{
    private readonly ISubjectService _subjects;

    public AdminSubjectsController(ISubjectService subjects)
    {
        _subjects = subjects;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var list = await _subjects.GetAllWithChaptersAsync(cancellationToken);
        return View(new AdminSubjectsIndexViewModel { Subjects = list });
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateSubjectViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSubjectViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (ok, err, subjectId) = await _subjects.CreateSubjectAsync(
            model.Code, model.Name, model.Description, cancellationToken);

        if (!ok)
        {
            ModelState.AddModelError(string.Empty, err);
            return View(model);
        }

        TempData["Success"] = $"Đã tạo môn {model.Code.Trim().ToUpperInvariant()}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> AddChapter(int subjectId, CancellationToken cancellationToken)
    {
        var subject = await _subjects.GetByIdAsync(subjectId, cancellationToken);
        if (subject is null)
            return NotFound();

        return View(new CreateSubjectChapterViewModel
        {
            SubjectId = subject.Id,
            SubjectCode = subject.Code,
            SubjectName = subject.Name
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddChapter(CreateSubjectChapterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            await _subjects.CreateChapterAsync(model.SubjectId, model.Title, cancellationToken);
            TempData["Success"] = "Đã thêm chương.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }
}
