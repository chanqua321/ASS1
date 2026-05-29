namespace Model.Entities;

public class Chapter
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OrderNumber { get; set; }

    public Subject Subject { get; set; } = null!;
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
