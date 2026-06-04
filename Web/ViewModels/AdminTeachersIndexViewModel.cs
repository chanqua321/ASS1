using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web.ViewModels;

public class AdminTeachersIndexViewModel
{
    [Required(ErrorMessage = "Nhập email giáo viên.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [ValidateNever]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Chọn môn cần gán.")]
    public int? SubjectId { get; set; }

    public List<SelectListItem> AvailableSubjects { get; set; } = new();
    public List<TeacherAssignmentRow> Teachers { get; set; } = new();
    public List<UnassignedSubjectRow> UnassignedSubjects { get; set; } = new();
}

public class TeacherAssignmentRow
{
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<string> Subjects { get; set; } = Array.Empty<string>();
}

public class UnassignedSubjectRow
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
