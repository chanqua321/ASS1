using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using BusinessLogic.DTOs;
using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;
using BusinessLogic.Options;
using Web.Helpers;
using Web.ViewModels;

namespace Web.Controllers;

/// <summary>
/// Lớp WEB — WF2 Chat RAG.
/// POST /Chat/Send nhận JSON (DTO) → IChatService (BusinessLogic) → trả JSON.
/// GET trả Razor View + ViewModel.
/// </summary>
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly ISubjectService _subjectService;
    private readonly IEnrollmentService _enrollmentService;
    private readonly IAiHealthService _aiHealth;
    private readonly IAuditService _audit;
    private readonly RagChatOptions _ragOptions;

    public ChatController(
        IChatService chatService,
        ISubjectService subjectService,
        IEnrollmentService enrollmentService,
        IAiHealthService aiHealth,
        IAuditService audit,
        IOptions<RagChatOptions> ragOptions)
    {
        _chatService = chatService;
        _subjectService = subjectService;
        _enrollmentService = enrollmentService;
        _aiHealth = aiHealth;
        _audit = audit;
        _ragOptions = ragOptions.Value;
    }

    // UI: mở trang Chat (GET /Chat) — hiển thị sessions + subjects + AI status
    [Authorize]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var subjects = await _subjectService.GetAllWithChaptersAsync(cancellationToken);
        var vm = new ChatIndexViewModel
        {
            Sessions = await _chatService.GetSessionsAsync(cancellationToken),
            Subjects = subjects.Select(s => new SelectListItem($"{s.Code} - {s.Name}", s.Id.ToString())).ToList(),
            AiStatus = await _aiHealth.GetStatusAsync(cancellationToken)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // UI: bấm "Phiên mới" (submit form) → tạo session → redirect sang /Chat/Session/{id}
    [Authorize]
    public async Task<IActionResult> Create(int? subjectId, CancellationToken cancellationToken)
    {
        var id = await _chatService.CreateSessionAsync(subjectId, cancellationToken);
        return RedirectToAction(nameof(Session), new { id });
    }

    // UI: bấm vào 1 phiên chat trong danh sách (GET /Chat/Session/{id})
    [Authorize]
    public async Task<IActionResult> Session(Guid id, CancellationToken cancellationToken)
    {
        var session = await _chatService.GetSessionAsync(id, cancellationToken);
        if (session is null)
            return NotFound();

        var canEnroll = session.SubjectId is > 0 &&
            await _enrollmentService.SubjectHasIndexedDocumentsAsync(session.SubjectId.Value, cancellationToken);

        var vm = new ChatSessionViewModel
        {
            Session = session,
            Messages = await _chatService.GetMessagesAsync(id, cancellationToken),
            AiStatus = await _aiHealth.GetStatusAsync(cancellationToken),
            IncludeCitationsByDefault = _ragOptions.IncludeCitationsByDefault,
            CanEnrollSubject = canEnroll,
            EnrollSubjectId = session.SubjectId
        };
        return View(vm);
    }

    [HttpPost]
    // UI: trang Session — bấm "Đăng ký" (JS fetch POST JSON) → /Chat/Enroll
    [Authorize]
    public async Task<IActionResult> Enroll([FromBody] SubjectEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _enrollmentService.EnrollAsync(request, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Message });

        return Json(result);
    }

    [HttpPost]
    // UI: trang Session — bấm "Gửi" (JS fetch POST JSON) → /Chat/Send
    [Authorize]
    public async Task<IActionResult> Send([FromBody] ChatSendRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "Câu hỏi không được để trống." });

        try
        {
            var response = await _chatService.SendAsync(request, cancellationToken);
            return Json(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // UI: bấm icon thùng rác "Xóa phiên" trong danh sách (POST /Chat/Delete/{id})
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _chatService.DeleteSessionAsync(id, cancellationToken);
        await AuditHttpHelper.LogAsync(
            HttpContext, _audit, AuditActions.ChatSessionDelete,
            $"SessionId={id}",
            cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
