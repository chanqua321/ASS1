namespace Model.Entities;

public class Subject
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Mỗi môn chỉ có 1 teacher phụ trách tài liệu (do Admin gán).
    public int? TeacherUserId { get; set; }
    public AppUser? TeacherUser { get; set; }

    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<SubjectEnrollment> Enrollments { get; set; } = new List<SubjectEnrollment>();
}
