using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web.ViewModels;

public class DocumentUploadViewModel : IValidatableObject
{
    public const string ModeExisting = "existing";
    public const string ModeNew = "new";

    [Required(ErrorMessage = "Vui lòng chọn file.")]
    [Display(Name = "Tài liệu (PDF, DOCX, PPTX)")]
    public IFormFile? File { get; set; }

    [Display(Name = "Cách chọn môn học")]
    public string SubjectInputMode { get; set; } = ModeExisting;

    [Display(Name = "Môn học có sẵn")]
    public int? SubjectId { get; set; }

    [Display(Name = "Tên môn / topic mới")]
    public string? NewSubjectName { get; set; }

    [Display(Name = "Mã môn (tuỳ chọn)")]
    public string? NewSubjectCode { get; set; }

    [Display(Name = "Chương có sẵn")]
    public int? ChapterId { get; set; }

    [Display(Name = "Hoặc tên chương mới")]
    public string? NewChapterTitle { get; set; }

    public IEnumerable<SelectListItem> Subjects { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Chapters { get; set; } = Array.Empty<SelectListItem>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SubjectInputMode == ModeNew)
        {
            if (string.IsNullOrWhiteSpace(NewSubjectName))
            {
                yield return new ValidationResult(
                    "Vui lòng nhập tên môn học / topic mới.",
                    [nameof(NewSubjectName)]);
            }
        }
        else if (SubjectId is null or <= 0)
        {
            yield return new ValidationResult(
                "Vui lòng chọn môn học.",
                [nameof(SubjectId)]);
        }

        if (ChapterId.HasValue && !string.IsNullOrWhiteSpace(NewChapterTitle))
        {
            yield return new ValidationResult(
                "Chỉ chọn chương có sẵn hoặc nhập tên chương mới, không dùng cả hai.",
                [nameof(ChapterId), nameof(NewChapterTitle)]);
        }
    }
}
