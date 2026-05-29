namespace Model.Entities;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Title { get; set; }
    public int? SubjectId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Subject? Subject { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
