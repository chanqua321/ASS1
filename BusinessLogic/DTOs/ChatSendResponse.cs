namespace BusinessLogic.DTOs;

public class ChatSendResponse
{
    public Guid SessionId { get; set; }
    public ChatMessageDto UserMessage { get; set; } = null!;
    public ChatMessageDto AssistantMessage { get; set; } = null!;
    public bool AnsweredFromDocuments { get; set; }
    public IReadOnlyList<DocumentLinkDto> DownloadableDocuments { get; set; } = Array.Empty<DocumentLinkDto>();
}
