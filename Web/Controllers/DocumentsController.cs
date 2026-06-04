using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using BusinessLogic.DTOs;
using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;
using BusinessLogic.Options;
using Web.Helpers;
using Web.ViewModels;

namespace Web.Controllers;

public class DocumentsController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly ISubjectService _subjectService;
    private readonly IAuditService _audit;
    private readonly IQuizService _quiz;
    private readonly RagChatOptions _ragOptions;

    public DocumentsController(
        IDocumentService documentService,
        ISubjectService subjectService,
        IAuditService audit,
        IQuizService quiz,
        IOptions<RagChatOptions> ragOptions)
    {
        _documentService = documentService;
        _subjectService = subjectService;
        _audit = audit;
        _quiz = quiz;
        _ragOptions = ragOptions.Value;
    }

    [Authorize]
    public async Task<IActionResult> Index(int? subjectId, CancellationToken cancellationToken)
    {
        int? teacherUserId = User.IsInRole("Teacher") ? GetCurrentUserId() : null;
        var vm = new DocumentListViewModel
        {
            FilterSubjectId = subjectId,
            Subjects = await BuildSubjectSelectListAsync(subjectId, teacherUserId, cancellationToken),
            Documents = await _documentService.GetProcessedDocumentsAsync(subjectId, teacherUserId, cancellationToken)
        };
        return View(vm);
    }

    [HttpGet]
    [Authorize(Policy = "TeacherOnly")]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        return View(await BuildUploadViewModelAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "TeacherOnly")]
    public async Task<IActionResult> Upload(DocumentUploadViewModel model, CancellationToken cancellationToken)
    {
        // Teacher chỉ được upload vào môn mình phụ trách
        if (model.SubjectInputMode == DocumentUploadViewModel.ModeNew)
        {
            ModelState.AddModelError(string.Empty, "Bạn không có quyền tạo môn mới khi upload. Hãy nhờ Admin tạo môn và gán teacher.");
            return View(await BuildUploadViewModelAsync(cancellationToken, model));
        }

        if (!ModelState.IsValid)
            return View(await BuildUploadViewModelAsync(cancellationToken, model));

        try
        {
            if (model.SubjectId is not > 0)
                throw new InvalidOperationException("Vui lòng chọn môn học.");

            var subject = await _subjectService.GetByIdAsync(model.SubjectId.Value, cancellationToken);
            if (subject is null)
                throw new InvalidOperationException("Môn học không tồn tại.");

            var currentUserId = GetCurrentUserId();
            if (subject.TeacherUserId != currentUserId)
                throw new InvalidOperationException("Bạn không có quyền upload tài liệu cho môn này.");

            await using var stream = model.File!.OpenReadStream();
            var request = new DocumentUploadRequest
            {
                FileName = model.File.FileName,
                ContentType = model.File.ContentType,
                FileSizeBytes = model.File.Length,
                FileStream = stream,
                SubjectId = model.SubjectId,
                NewSubjectName = null,
                NewSubjectCode = null,
                ChapterId = model.ChapterId,
                NewChapterTitle = model.NewChapterTitle
            };

            var created = await _documentService.UploadAsync(request, cancellationToken);
            await AuditHttpHelper.LogAsync(
                HttpContext, _audit, AuditActions.DocumentUpload,
                $"DocumentId={created.Id}; File={created.FileName}",
                cancellationToken);
            TempData["Success"] = "Tài liệu đã được tải lên, lập chỉ mục và tạo tóm tắt AI.";
            // Upload xong chuyển qua trang Details để xem nội dung trích xuất.
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildUploadViewModelAsync(cancellationToken, model));
        }
    }

    [Authorize]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (!await CanAccessDocumentAsync(id, cancellationToken))
            return Forbid();

        var doc = await _documentService.GetDetailsAsync(id, cancellationToken);
        if (doc is null)
            return NotFound();

        var canQuiz = User.IsInRole("Teacher") && doc.IsIndexed;
        var vm = new DocumentDetailsPageViewModel
        {
            Document = doc,
            LatestQuiz = canQuiz ? await _quiz.GetLatestAsync(id, cancellationToken) : null,
            QuizHistory = canQuiz ? await _quiz.GetHistoryAsync(id, 8, cancellationToken) : Array.Empty<DocumentQuizDto>(),
            CanManageQuiz = canQuiz,
            QuizDefaultQuestionCount = _ragOptions.QuizDefaultQuestionCount,
            QuizMinQuestionCount = _ragOptions.QuizMinQuestionCount,
            QuizMaxQuestionCount = _ragOptions.QuizMaxQuestionCount
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "TeacherOnly")]
    public async Task<IActionResult> GenerateQuiz(int id, int questionCount, CancellationToken cancellationToken)
    {
        if (!await CanAccessDocumentAsync(id, cancellationToken))
            return Forbid();

        questionCount = Math.Clamp(
            questionCount,
            _ragOptions.QuizMinQuestionCount,
            _ragOptions.QuizMaxQuestionCount);

        var (ok, error, quiz) = await _quiz.GenerateAsync(id, GetCurrentUserId(), questionCount, cancellationToken);
        if (!ok)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Details), new { id });
        }

        var actualCount = quiz?.Questions.Count ?? questionCount;
        await AuditHttpHelper.LogAsync(
            HttpContext, _audit, AuditActions.GenerateQuiz,
            $"DocumentId={id}; Questions={actualCount}",
            cancellationToken);
        TempData["Success"] = $"Đã sinh bộ quiz mới ({actualCount} câu trắc nghiệm).";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = "TeacherOnly")]
    public async Task<IActionResult> ExportQuiz(int quizId, string format = "pdf", CancellationToken cancellationToken = default)
    {
        var quiz = await _quiz.GetByIdAsync(quizId, cancellationToken);
        if (quiz is null)
            return NotFound();

        if (!await CanAccessDocumentAsync(quiz.DocumentId, cancellationToken))
            return Forbid();

        var content = _quiz.BuildExportContent(quiz, format);
        var baseName = Path.GetFileNameWithoutExtension(quiz.DocumentFileName);

        if (format.Equals("word", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            return File(bytes, "application/msword", $"{baseName}-quiz.doc");
        }

        var pdfBytes = Encoding.UTF8.GetBytes(content);
        return File(pdfBytes, "application/pdf", $"{baseName}-quiz.pdf");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "TeacherOnly")]
    public async Task<IActionResult> Reindex(int id, CancellationToken cancellationToken)
    {
        if (!await CanAccessDocumentAsync(id, cancellationToken))
            return Forbid();

        if (!await _documentService.ExistsAsync(id, cancellationToken))
            return NotFound();

        await _documentService.ProcessDocumentAsync(id, cancellationToken);
        await AuditHttpHelper.LogAsync(
            HttpContext, _audit, AuditActions.DocumentReindex,
            $"DocumentId={id}",
            cancellationToken);
        TempData["Success"] = "Đã xử lý lại tài liệu. Bạn có thể chat tóm tắt.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        if (!await CanAccessDocumentAsync(id, cancellationToken))
            return Forbid();

        var download = await _documentService.GetDownloadAsync(id, cancellationToken);
        if (download is null)
            return NotFound();

        await AuditHttpHelper.LogAsync(
            HttpContext, _audit, AuditActions.DocumentDownload,
            $"DocumentId={id}; File={download.FileName}",
            cancellationToken);

        return File(download.FileStream, download.ContentType, download.FileName);
    }

    [HttpGet]
    [Authorize(Policy = "TeacherOnly")]
    public async Task<IActionResult> ChaptersBySubject(int subjectId, CancellationToken cancellationToken)
    {
        var subject = await _subjectService.GetByIdAsync(subjectId, cancellationToken);
        if (subject is null)
            return Json(Array.Empty<object>());

        if (subject.TeacherUserId != GetCurrentUserId())
            return Forbid();

        var chapters = subject.Chapters
            .OrderBy(c => c.OrderNumber)
            .Select(c => new { c.Id, c.Title });

        return Json(chapters);
    }

    private async Task<IEnumerable<SelectListItem>> BuildSubjectSelectListAsync(
        int? selectedId,
        int? teacherUserId,
        CancellationToken cancellationToken)
    {
        var subjects = await _subjectService.GetAllWithChaptersAsync(cancellationToken);
        if (teacherUserId.HasValue)
            subjects = subjects.Where(s => s.TeacherUserId == teacherUserId).ToList();

        return subjects
            .OrderBy(s => s.Code)
            .Select(s => new SelectListItem($"{s.Code} — {s.Name}", s.Id.ToString(), selectedId == s.Id));
    }

    private async Task<bool> CanAccessDocumentAsync(int documentId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Teacher"))
            return true;

        return await _documentService.TeacherCanAccessAsync(documentId, GetCurrentUserId(), cancellationToken);
    }

    private async Task<DocumentUploadViewModel> BuildUploadViewModelAsync(
        CancellationToken cancellationToken,
        DocumentUploadViewModel? model = null)
    {
        var subjects = await _subjectService.GetAllWithChaptersAsync(cancellationToken);
        var vm = model ?? new DocumentUploadViewModel();
        var currentUserId = GetCurrentUserId();
        var allowed = subjects.Where(s => s.TeacherUserId == currentUserId).ToList();
        vm.Subjects = allowed.Select(s => new SelectListItem($"{s.Code} - {s.Name}", s.Id.ToString())).ToList();

        if (vm.SubjectInputMode == DocumentUploadViewModel.ModeExisting && vm.SubjectId is > 0)
        {
            var selected = allowed.FirstOrDefault(s => s.Id == vm.SubjectId);
            vm.Chapters = selected?.Chapters
                .OrderBy(c => c.OrderNumber)
                .Select(c => new SelectListItem(c.Title, c.Id.ToString(), vm.ChapterId == c.Id))
                .ToList() ?? new List<SelectListItem>();
        }

        return vm;
    }

    private int GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(raw, out var id))
            throw new InvalidOperationException("Không xác định được tài khoản đăng nhập.");
        return id;
    }
}
