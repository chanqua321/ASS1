namespace Service.DTOs;

public class CitationDto
{
    public int DocumentId { get; set; }
    public int ChunkId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ChapterTitle { get; set; }
    public string Excerpt { get; set; } = string.Empty;
    public double Score { get; set; }
}
