using Model.Enums;

namespace Service.DTOs;

public class ChatMessageDto
{
    public int Id { get; set; }
    public ChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<CitationDto> Citations { get; set; } = Array.Empty<CitationDto>();
    public IReadOnlyList<DocumentLinkDto> DownloadableDocuments { get; set; } = Array.Empty<DocumentLinkDto>();
}
