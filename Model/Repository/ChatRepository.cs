using Model.IRepository;
using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Entities;

namespace Model.Repository;

public class ChatRepository(AppDbContext db) : IChatRepository
{
    public Task<List<ChatSession>> GetSessionsWithSubjectAsync(CancellationToken cancellationToken = default) =>
        db.ChatSessions
            .AsNoTracking()
            .Include(s => s.Subject)
            .Include(s => s.Messages)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(cancellationToken);

    public Task<ChatSession?> GetSessionByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.ChatSessions
            .AsNoTracking()
            .Include(x => x.Subject)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<int> CountMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        db.ChatMessages.CountAsync(m => m.SessionId == sessionId, cancellationToken);

    public Task<List<ChatMessage>> GetMessagesWithCitationsAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        db.ChatMessages
            .AsNoTracking()
            .Include(m => m.Citations)
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ChatSession?> GetSessionForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.ChatSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task AddSessionAsync(ChatSession session, CancellationToken cancellationToken = default) =>
        await db.ChatSessions.AddAsync(session, cancellationToken);

    public async Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default) =>
        await db.ChatMessages.AddAsync(message, cancellationToken);

    public Task<List<ChatMessage>> GetHistoryBeforeMessageAsync(
        Guid sessionId,
        int beforeMessageId,
        int take,
        CancellationToken cancellationToken = default) =>
        db.ChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.Id < beforeMessageId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task DeleteSessionAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        db.ChatSessions.Remove(session);
        return Task.CompletedTask;
    }
}
