namespace Service.DTOs;

public class SubjectEnrollmentRequest
{
    public int SubjectId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
