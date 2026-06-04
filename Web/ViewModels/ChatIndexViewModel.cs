using Microsoft.AspNetCore.Mvc.Rendering;
using BusinessLogic.DTOs;

namespace Web.ViewModels;

public class ChatIndexViewModel
{
    public IReadOnlyList<ChatSessionDto> Sessions { get; set; } = Array.Empty<ChatSessionDto>();
    public IEnumerable<SelectListItem> Subjects { get; set; } = Array.Empty<SelectListItem>();
    public AiStatusDto AiStatus { get; set; } = new();
}
