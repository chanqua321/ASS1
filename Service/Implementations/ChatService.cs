using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Model.Data;
using Model.Entities;
using Model.Enums;
using Service.DTOs;
using Service.Helpers;
using Service.Interfaces;
using Service.Options;

namespace Service.Implementations;

public class ChatService : IChatService
{
    private readonly AppDbContext _db;
    private readonly IRetrievalService _retrieval;
    private readonly IRagAnswerGenerator _answerGenerator;
    private readonly RagChatOptions _options;

    public ChatService(
        AppDbContext db,
        IRetrievalService retrieval,
        IRagAnswerGenerator answerGenerator,
        IOptions<RagChatOptions> options)
    {
        _db = db;
        _retrieval = retrieval;
        _answerGenerator = answerGenerator;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<ChatSessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.ChatSessions
            .AsNoTracking()
            .Include(s => s.Subject)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new ChatSessionDto
            {
                Id = s.Id,
                Title = s.Title ?? "Phiên chat mới",
                SubjectId = s.SubjectId,
                SubjectName = s.Subject != null ? s.Subject.Name : null,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                MessageCount = s.Messages.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ChatSessionDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var s = await _db.ChatSessions
            .AsNoTracking()
            .Include(x => x.Subject)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (s is null) return null;

        var count = await _db.ChatMessages.CountAsync(m => m.SessionId == sessionId, cancellationToken);

        return new ChatSessionDto
        {
            Id = s.Id,
            Title = s.Title ?? "Phiên chat mới",
            SubjectId = s.SubjectId,
            SubjectName = s.Subject?.Name,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            MessageCount = count
        };
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var messages = await _db.ChatMessages
            .AsNoTracking()
            .Include(m => m.Citations)
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return messages.Select(MapMessage).ToList();
    }

    public async Task<ChatSendResponse> SendAsync(
        ChatSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var question = request.Question?.Trim()
            ?? throw new ArgumentException("Câu hỏi không được để trống.");

        ChatSession session;
        if (request.SessionId.HasValue)
        {
            session = await _db.ChatSessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Phiên chat không tồn tại.");
        }
        else
        {
            session = new ChatSession
            {
                SubjectId = request.SubjectId,
                Title = TruncateTitle(question)
            };
            _db.ChatSessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (request.SubjectId.HasValue && session.SubjectId != request.SubjectId)
            session.SubjectId = request.SubjectId;

        var userMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = ChatMessageRole.User,
            Content = question
        };
        _db.ChatMessages.Add(userMessage);
        await _db.SaveChangesAsync(cancellationToken);

        var history = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == session.Id && m.Id < userMessage.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(_options.MaxHistoryMessages)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var recentConversation = history
            .Select(m => $"{m.Role}: {m.Content}")
            .ToList();

        if (ConversationalIntentHelper.TryGetReply(question, out var casualAnswer))
        {
            var casualAssistant = new ChatMessage
            {
                SessionId = session.Id,
                Role = ChatMessageRole.Assistant,
                Content = casualAnswer
            };
            _db.ChatMessages.Add(casualAssistant);
            session.UpdatedAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(session.Title) || session.Title == "Phiên chat mới")
                session.Title = TruncateTitle(question);
            await _db.SaveChangesAsync(cancellationToken);

            return new ChatSendResponse
            {
                SessionId = session.Id,
                UserMessage = MapMessage(userMessage),
                AssistantMessage = MapMessage(casualAssistant),
                AnsweredFromDocuments = false,
                DownloadableDocuments = Array.Empty<DocumentLinkDto>()
            };
        }

        var isSummary = RagChunkSelector.IsSummaryQuestion(question);
        var chunks = isSummary
            ? await _retrieval.RetrieveForSummaryAsync(
                question,
                session.SubjectId,
                _options.SummaryTopK,
                cancellationToken)
            : await _retrieval.RetrieveAsync(
                question,
                session.SubjectId,
                _options.TopK,
                cancellationToken);

        var includeCitations = request.IncludeCitations ?? _options.IncludeCitationsByDefault;

        var (answer, fromDocuments) = await _answerGenerator.GenerateAsync(
            question,
            chunks,
            recentConversation,
            includeCitations,
            isSummary,
            cancellationToken);

        var assistantMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = ChatMessageRole.Assistant,
            Content = answer
        };

        var downloadable = BuildDownloadableDocuments(chunks);

        if (fromDocuments)
        {
            foreach (var doc in downloadable)
            {
                var chunk = chunks
                    .Where(c => c.DocumentId == doc.DocumentId)
                    .OrderByDescending(c => c.Score)
                    .First();

                var showExcerpt = includeCitations &&
                    chunk.Score >= _options.MinCitationScore;

                assistantMessage.Citations.Add(new MessageCitation
                {
                    DocumentId = chunk.DocumentId,
                    ChunkId = chunk.ChunkId,
                    FileName = chunk.FileName,
                    ChapterTitle = chunk.ChapterTitle,
                    Excerpt = showExcerpt ? TruncateExcerpt(chunk.Content) : string.Empty,
                    Score = chunk.Score
                });
            }
        }

        _db.ChatMessages.Add(assistantMessage);

        session.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(session.Title) || session.Title == "Phiên chat mới")
            session.Title = TruncateTitle(question);

        await _db.SaveChangesAsync(cancellationToken);

        var assistantDto = MapMessage(assistantMessage);

        return new ChatSendResponse
        {
            SessionId = session.Id,
            UserMessage = MapMessage(userMessage),
            AssistantMessage = assistantDto,
            AnsweredFromDocuments = fromDocuments,
            DownloadableDocuments = assistantDto.DownloadableDocuments
        };
    }

    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _db.ChatSessions.FindAsync([sessionId], cancellationToken);
        if (session is null) return;

        _db.ChatSessions.Remove(session);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> CreateSessionAsync(int? subjectId, CancellationToken cancellationToken = default)
    {
        var session = new ChatSession { SubjectId = subjectId, Title = "Phiên chat mới" };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    private string TruncateExcerpt(string content) =>
        content.Length <= _options.MaxExcerptLength
            ? content
            : content[.._options.MaxExcerptLength] + "...";

    private static string TruncateTitle(string question)
    {
        var title = question.Trim();
        return title.Length <= 80 ? title : title[..80] + "...";
    }

    private static IReadOnlyList<DocumentLinkDto> BuildDownloadableDocuments(
        IReadOnlyList<RetrievedChunkDto> chunks) =>
        chunks
            .OrderByDescending(c => c.Score)
            .GroupBy(c => c.DocumentId)
            .Select(g =>
            {
                var c = g.First();
                return new DocumentLinkDto
                {
                    DocumentId = c.DocumentId,
                    FileName = c.FileName,
                    ChapterTitle = c.ChapterTitle,
                    Score = g.Max(x => x.Score)
                };
            })
            .Take(5)
            .ToList();

    private static ChatMessageDto MapMessage(ChatMessage m)
    {
        var downloads = m.Citations
            .GroupBy(c => c.DocumentId)
            .Select(g =>
            {
                var c = g.First();
                return new DocumentLinkDto
                {
                    DocumentId = c.DocumentId,
                    FileName = c.FileName,
                    ChapterTitle = c.ChapterTitle,
                    Score = g.Max(x => x.Score)
                };
            })
            .ToList();

        return new ChatMessageDto
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            CreatedAt = m.CreatedAt,
            DownloadableDocuments = downloads,
            Citations = m.Citations
                .Where(c => !string.IsNullOrWhiteSpace(c.Excerpt))
                .Select(c => new CitationDto
                {
                    DocumentId = c.DocumentId,
                    ChunkId = c.ChunkId,
                    FileName = c.FileName,
                    ChapterTitle = c.ChapterTitle,
                    Excerpt = c.Excerpt,
                    Score = c.Score
                })
                .ToList()
        };
    }
}
