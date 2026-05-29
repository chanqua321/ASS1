namespace Model.Entities;

public class MessageCitation
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public int DocumentId { get; set; }
    public int ChunkId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ChapterTitle { get; set; }
    public string Excerpt { get; set; } = string.Empty;
    public double Score { get; set; }

    public ChatMessage Message { get; set; } = null!;
}
