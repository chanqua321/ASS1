using Microsoft.AspNetCore.Mvc.Rendering;
using BusinessLogic.DTOs;

namespace Web.ViewModels;

public class ChatSessionViewModel
{
    public ChatSessionDto Session { get; set; } = null!;
    public IReadOnlyList<ChatMessageDto> Messages { get; set; } = Array.Empty<ChatMessageDto>();
    public IEnumerable<SelectListItem> Subjects { get; set; } = Array.Empty<SelectListItem>();
    public AiStatusDto AiStatus { get; set; } = new();
    public bool IncludeCitationsByDefault { get; set; }
    public bool CanEnrollSubject { get; set; }
    public int? EnrollSubjectId { get; set; }
}
