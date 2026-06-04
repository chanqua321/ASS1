using Model.Entities;

namespace Model.IRepository;

public interface IChatRepository
{
    Task<List<ChatSession>> GetSessionsWithSubjectAsync(CancellationToken cancellationToken = default);
    Task<ChatSession?> GetSessionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<List<ChatMessage>> GetMessagesWithCitationsAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<ChatSession?> GetSessionForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddSessionAsync(ChatSession session, CancellationToken cancellationToken = default);
    Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);
    Task<List<ChatMessage>> GetHistoryBeforeMessageAsync(Guid sessionId, int beforeMessageId, int take, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(ChatSession session, CancellationToken cancellationToken = default);
}
