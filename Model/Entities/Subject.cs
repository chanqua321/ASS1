namespace Model.Entities;

public class Subject
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Quan hệ gán môn: 1 môn ↔ tối đa 1 teacher; 1 teacher ↔ nhiều môn (Admin gán qua TeacherUserId).
    public int? TeacherUserId { get; set; }
    public AppUser? TeacherUser { get; set; }

    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<SubjectEnrollment> Enrollments { get; set; } = new List<SubjectEnrollment>();
}
