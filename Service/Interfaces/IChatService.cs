using Service.DTOs;

namespace Service.Interfaces;

public interface IChatService
{
    Task<IReadOnlyList<ChatSessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<ChatSessionDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<ChatSendResponse> SendAsync(ChatSendRequest request, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<Guid> CreateSessionAsync(int? subjectId, CancellationToken cancellationToken = default);
}
