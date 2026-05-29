namespace Service.DTOs;

public class SubjectEnrollmentResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SubjectName { get; set; }
}
