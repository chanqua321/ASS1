using Microsoft.AspNetCore.Mvc.Rendering;
using Service.DTOs;

namespace Web.ViewModels;

public class ChatIndexViewModel
{
    public IReadOnlyList<ChatSessionDto> Sessions { get; set; } = Array.Empty<ChatSessionDto>();
    public IEnumerable<SelectListItem> Subjects { get; set; } = Array.Empty<SelectListItem>();
    public int? DefaultSubjectId { get; set; } = 1;
    public AiStatusDto AiStatus { get; set; } = new();
}
