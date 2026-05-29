namespace Service.DTOs;

public class ChatSendRequest
{
    public Guid? SessionId { get; set; }
    public string Question { get; set; } = string.Empty;
    public int? SubjectId { get; set; }

    /// <summary>null = dùng cấu hình Chat:Rag:IncludeCitationsByDefault</summary>
    public bool? IncludeCitations { get; set; }
}
