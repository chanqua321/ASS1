using System.ComponentModel.DataAnnotations;

namespace Web.ViewModels;

public class CreateSubjectChapterViewModel
{
    public int SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Thiếu tên chương.")]
    [StringLength(300)]
    [Display(Name = "Tên chương")]
    public string Title { get; set; } = string.Empty;
}
