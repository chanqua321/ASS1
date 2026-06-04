using System.ComponentModel.DataAnnotations;

namespace Web.ViewModels;

public class CreateSubjectViewModel
{
    [Required(ErrorMessage = "Thiếu mã môn (Code).")]
    [StringLength(20)]
    [Display(Name = "Mã môn (Code)")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Thiếu tên môn.")]
    [StringLength(200)]
    [Display(Name = "Tên môn")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    public string? Description { get; set; }
}
