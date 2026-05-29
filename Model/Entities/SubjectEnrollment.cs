namespace Model.Entities;

public class SubjectEnrollment
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    public Subject Subject { get; set; } = null!;
}
