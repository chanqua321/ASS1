namespace Service.DTOs;

public class RetrievedChunkDto
{
    public int ChunkId { get; set; }
    public int DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ChapterTitle { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
}
