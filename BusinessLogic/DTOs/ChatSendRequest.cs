namespace BusinessLogic.DTOs;

/// <summary>
/// Request — POST /Chat/Send (application/json).
/// Body: sessionId?, question, subjectId?, includeCitations?
/// </summary>
public class ChatSendRequest
{
    public Guid? SessionId { get; set; }
    public string Question { get; set; } = string.Empty;
    public int? SubjectId { get; set; }
    public bool? IncludeCitations { get; set; }
}
