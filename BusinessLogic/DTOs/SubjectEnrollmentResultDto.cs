namespace BusinessLogic.DTOs;

/// <summary>
/// Response — POST /Chat/Enroll → 200 application/json.
/// Body: success, message, subjectName.
/// Lỗi: 400 { "error": "..." }.
/// </summary>
public class SubjectEnrollmentResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SubjectName { get; set; }
}
