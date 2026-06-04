namespace Model.Entities;

public class DocumentQuiz
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int CreatedByUserId { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
    public AppUser CreatedByUser { get; set; } = null!;
}
