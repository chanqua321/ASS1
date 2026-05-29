using Model.Enums;

namespace Model.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public ChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ChatSession Session { get; set; } = null!;
    public ICollection<MessageCitation> Citations { get; set; } = new List<MessageCitation>();
}
