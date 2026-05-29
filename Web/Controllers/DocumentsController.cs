using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Service.DTOs;
using Service.Interfaces;
using Web.ViewModels;

namespace Web.Controllers;

public class DocumentsController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly ISubjectService _subjectService;

    public DocumentsController(IDocumentService documentService, ISubjectService subjectService)
    {
        _documentService = documentService;
        _subjectService = subjectService;
    }

    public async Task<IActionResult> Index(int? subjectId, CancellationToken cancellationToken)
    {
        var subjects = await _subjectService.GetAllWithChaptersAsync(cancellationToken);
        var vm = new DocumentListViewModel
        {
            FilterSubjectId = subjectId,
            Subjects = subjects.Select(s => new SelectListItem(s.Name, s.Id.ToString(), subjectId == s.Id)).ToList(),
            Documents = await _documentService.GetProcessedDocumentsAsync(subjectId, cancellationToken)
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        return View(await BuildUploadViewModelAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(DocumentUploadViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(await BuildUploadViewModelAsync(cancellationToken, model));

        try
        {
            await using var stream = model.File!.OpenReadStream();
            var request = new DocumentUploadRequest
            {
                FileName = model.File.FileName,
                ContentType = model.File.ContentType,
                FileSizeBytes = model.File.Length,
                FileStream = stream,
                SubjectId = model.SubjectInputMode == DocumentUploadViewModel.ModeExisting ? model.SubjectId : null,
                NewSubjectName = model.SubjectInputMode == DocumentUploadViewModel.ModeNew ? model.NewSubjectName : null,
                NewSubjectCode = model.SubjectInputMode == DocumentUploadViewModel.ModeNew ? model.NewSubjectCode : null,
                ChapterId = model.ChapterId,
                NewChapterTitle = model.NewChapterTitle
            };

            await _documentService.UploadAsync(request, cancellationToken);
            TempData["Success"] = "Tài liệu đã được tải lên và xử lý (chunk + embed + index).";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildUploadViewModelAsync(cancellationToken, model));
        }
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var doc = await _documentService.GetByIdAsync(id, cancellationToken);
        if (doc is null)
            return NotFound();

        return View(doc);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reindex(int id, CancellationToken cancellationToken)
    {
        var doc = await _documentService.GetByIdAsync(id, cancellationToken);
        if (doc is null)
            return NotFound();

        await _documentService.ProcessDocumentAsync(id, cancellationToken);
        TempData["Success"] = "Đã index lại tài liệu (đọc nội dung mới từ file). Bạn có thể chat tóm tắt.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var download = await _documentService.GetDownloadAsync(id, cancellationToken);
        if (download is null)
            return NotFound();

        return File(download.FileStream, download.ContentType, download.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> ChaptersBySubject(int subjectId, CancellationToken cancellationToken)
    {
        var subject = await _subjectService.GetByIdAsync(subjectId, cancellationToken);
        if (subject is null)
            return Json(Array.Empty<object>());

        var chapters = subject.Chapters
            .OrderBy(c => c.OrderNumber)
            .Select(c => new { c.Id, c.Title });

        return Json(chapters);
    }

    private async Task<DocumentUploadViewModel> BuildUploadViewModelAsync(
        CancellationToken cancellationToken,
        DocumentUploadViewModel? model = null)
    {
        var subjects = await _subjectService.GetAllWithChaptersAsync(cancellationToken);
        var vm = model ?? new DocumentUploadViewModel();
        vm.Subjects = subjects.Select(s => new SelectListItem($"{s.Code} - {s.Name}", s.Id.ToString())).ToList();

        if (vm.SubjectInputMode == DocumentUploadViewModel.ModeExisting && vm.SubjectId is > 0)
        {
            var selected = subjects.FirstOrDefault(s => s.Id == vm.SubjectId);
            vm.Chapters = selected?.Chapters
                .OrderBy(c => c.OrderNumber)
                .Select(c => new SelectListItem(c.Title, c.Id.ToString(), vm.ChapterId == c.Id))
                .ToList() ?? new List<SelectListItem>();
        }

        return vm;
    }
}
