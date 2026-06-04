namespace BusinessLogic.DTOs;

public class DocumentLinkDto
{
    public int DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ChapterTitle { get; set; }
    public double Score { get; set; }
}
