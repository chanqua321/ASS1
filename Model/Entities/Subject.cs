namespace Model.Entities;

public class Subject
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<SubjectEnrollment> Enrollments { get; set; } = new List<SubjectEnrollment>();
}
