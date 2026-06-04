namespace BusinessLogic.DTOs;

/// <summary>
/// Response — POST /Chat/Send → 200 application/json.
/// Body: sessionId, userMessage, assistantMessage, answeredFromDocuments, downloadableDocuments[].
/// Lỗi: 400 { "error": "..." }.
/// </summary>
public class ChatSendResponse
{
    public Guid SessionId { get; set; }
    public ChatMessageDto UserMessage { get; set; } = null!;
    public ChatMessageDto AssistantMessage { get; set; } = null!;
    public bool AnsweredFromDocuments { get; set; }
    public IReadOnlyList<DocumentLinkDto> DownloadableDocuments { get; set; } = Array.Empty<DocumentLinkDto>();
}
