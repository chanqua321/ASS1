using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using Service.DTOs;
using Service.Interfaces;
using Service.Options;
using Web.ViewModels;

namespace Web.Controllers;

public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly ISubjectService _subjectService;
    private readonly IEnrollmentService _enrollmentService;
    private readonly IAiHealthService _aiHealth;
    private readonly RagChatOptions _ragOptions;

    public ChatController(
        IChatService chatService,
        ISubjectService subjectService,
        IEnrollmentService enrollmentService,
        IAiHealthService aiHealth,
        IOptions<RagChatOptions> ragOptions)
    {
        _chatService = chatService;
        _subjectService = subjectService;
        _enrollmentService = enrollmentService;
        _aiHealth = aiHealth;
        _ragOptions = ragOptions.Value;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var subjects = await _subjectService.GetAllWithChaptersAsync(cancellationToken);
        var vm = new ChatIndexViewModel
        {
            Sessions = await _chatService.GetSessionsAsync(cancellationToken),
            Subjects = subjects.Select(s => new SelectListItem($"{s.Code} - {s.Name}", s.Id.ToString())).ToList(),
            DefaultSubjectId = subjects.FirstOrDefault()?.Id ?? 1,
            AiStatus = await _aiHealth.GetStatusAsync(cancellationToken)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int? subjectId, CancellationToken cancellationToken)
    {
        var id = await _chatService.CreateSessionAsync(subjectId, cancellationToken);
        return RedirectToAction(nameof(Session), new { id });
    }

    public async Task<IActionResult> Session(Guid id, CancellationToken cancellationToken)
    {
        var session = await _chatService.GetSessionAsync(id, cancellationToken);
        if (session is null)
            return NotFound();

        var subjects = await _subjectService.GetAllWithChaptersAsync(cancellationToken);
        var canEnroll = session.SubjectId is > 0 &&
            await _enrollmentService.SubjectHasIndexedDocumentsAsync(session.SubjectId.Value, cancellationToken);

        var vm = new ChatSessionViewModel
        {
            Session = session,
            Messages = await _chatService.GetMessagesAsync(id, cancellationToken),
            Subjects = subjects.Select(s => new SelectListItem(
                $"{s.Code} - {s.Name}",
                s.Id.ToString(),
                session.SubjectId == s.Id)).ToList(),
            AiStatus = await _aiHealth.GetStatusAsync(cancellationToken),
            IncludeCitationsByDefault = _ragOptions.IncludeCitationsByDefault,
            CanEnrollSubject = canEnroll,
            EnrollSubjectId = session.SubjectId
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Enroll([FromBody] SubjectEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _enrollmentService.EnrollAsync(request, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Message });

        return Json(result);
    }

    [HttpPost]
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
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _chatService.DeleteSessionAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
