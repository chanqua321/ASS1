using Microsoft.AspNetCore.Mvc.Rendering;
using BusinessLogic.DTOs;

namespace Web.ViewModels;

public class DocumentListViewModel
{
    public int? FilterSubjectId { get; set; }
    public IEnumerable<SelectListItem> Subjects { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<DocumentListItemDto> Documents { get; set; } = Array.Empty<DocumentListItemDto>();
}
