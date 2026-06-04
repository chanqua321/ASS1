namespace BusinessLogic.DTOs;

/// <summary>
/// Request — POST /Chat/Enroll (application/json).
/// Body: subjectId, fullName, email.
/// </summary>
public class SubjectEnrollmentRequest
{
    public int SubjectId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
