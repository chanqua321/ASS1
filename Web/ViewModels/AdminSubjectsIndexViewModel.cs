using BusinessLogic.DTOs;

namespace Web.ViewModels;

public class AdminSubjectsIndexViewModel
{
    public IReadOnlyList<SubjectListItemDto> Subjects { get; set; } = Array.Empty<SubjectListItemDto>();
}
