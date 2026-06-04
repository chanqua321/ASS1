using Microsoft.Extensions.Options;
using Model.Entities;
using Model.Enums;
using Model.IRepository;
using Model.IUnitOfWork;
using BusinessLogic.DTOs;
using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;
using BusinessLogic.Options;

namespace BusinessLogic.Logic;

public class ChatService : IChatService
{
    private readonly IChatRepository _chat;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRetrievalService _retrieval;
    private readonly IRagAnswerGenerator _answerGenerator;
    private readonly RagChatOptions _options;

    public ChatService(
        IChatRepository chat,
        IUnitOfWork unitOfWork,
        IRetrievalService retrieval,
        IRagAnswerGenerator answerGenerator,
        IOptions<RagChatOptions> options)
    {
        _chat = chat;
        _unitOfWork = unitOfWork;
        _retrieval = retrieval;
        _answerGenerator = answerGenerator;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<ChatSessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await _chat.GetSessionsWithSubjectAsync(cancellationToken);
        return sessions.Select(s => new ChatSessionDto
        {
            Id = s.Id,
            Title = s.Title ?? "Phiên chat mới",
            SubjectId = s.SubjectId,
            SubjectName = s.Subject?.Name,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            MessageCount = s.Messages.Count
        }).ToList();
    }

    public async Task<ChatSessionDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var s = await _chat.GetSessionByIdAsync(sessionId, cancellationToken);
        if (s is null) return null;

        var count = await _chat.CountMessagesAsync(sessionId, cancellationToken);

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
        var messages = await _chat.GetMessagesWithCitationsAsync(sessionId, cancellationToken);
        return messages.Select(MapMessage).ToList();
    }

    public async Task<ChatSendResponse> SendAsync(
        ChatSendRequest request,
        CancellationToken cancellationToken = default)
    {
        // WF2 (Chat RAG):
        // - Casual intent: trả lời nhanh (không retrieve/ollama)
        // - RAG intent: save user → retrieve chunks → call Ollama → save assistant + citations → return JSON
        var question = request.Question?.Trim()
            ?? throw new ArgumentException("Câu hỏi không được để trống.");

        ChatSession session;
        if (request.SessionId.HasValue)
        {
            // Update existing session
            session = await _chat.GetSessionForUpdateAsync(request.SessionId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Phiên chat không tồn tại.");
        }
        else
        {
            // Create new session (cần SaveChanges để có session.Id)
            session = new ChatSession
            {
                SubjectId = request.SubjectId,
                Title = TruncateTitle(question)
            };
            await _chat.AddSessionAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (request.SubjectId.HasValue && session.SubjectId != request.SubjectId)
            session.SubjectId = request.SubjectId;

        if (ConversationalIntentHelper.TryGetReply(question, out var casualAnswer))
        {
            // Nhánh "casual": chỉ lưu 2 message (user + assistant) và trả về ngay.
            var casualUser = new ChatMessage
            {
                SessionId = session.Id,
                Role = ChatMessageRole.User,
                Content = question
            };
            var casualAssistant = new ChatMessage
            {
                SessionId = session.Id,
                Role = ChatMessageRole.Assistant,
                Content = casualAnswer
            };
            await _chat.AddMessageAsync(casualUser, cancellationToken);
            await _chat.AddMessageAsync(casualAssistant, cancellationToken);
            session.UpdatedAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(session.Title) || session.Title == "Phiên chat mới")
                session.Title = TruncateTitle(question);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ChatSendResponse
            {
                SessionId = session.Id,
                UserMessage = MapMessage(casualUser),
                AssistantMessage = MapMessage(casualAssistant),
                AnsweredFromDocuments = false,
                DownloadableDocuments = Array.Empty<DocumentLinkDto>()
            };
        }

        // Nhánh RAG: cần lưu user trước để có userMessage.Id (phục vụ lấy history "trước message này").
        var userMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = ChatMessageRole.User,
            Content = question
        };
        await _chat.AddMessageAsync(userMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Lấy history gần nhất để model trả lời “liền mạch” (giới hạn MaxHistoryMessages).
        var history = await _chat.GetHistoryBeforeMessageAsync(
            session.Id, userMessage.Id, _options.MaxHistoryMessages, cancellationToken);

        var recentConversation = history
            .Select(m => $"{m.Role}: {m.Content}")
            .ToList();

        // Detect câu hỏi tóm tắt để dùng đường retrieve/summary riêng.
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

        // Generate: gọi Ollama nếu available; fail thì fallback local excerpt/summary.
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

        // Citation: chỉ thêm excerpt khi user bật includeCitations và score đủ cao.
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

        await _chat.AddMessageAsync(assistantMessage, cancellationToken);

        // SaveChanges cuối: lưu assistant + citations + update session title/time.
        session.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(session.Title) || session.Title == "Phiên chat mới")
            session.Title = TruncateTitle(question);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
        var session = await _chat.GetSessionForUpdateAsync(sessionId, cancellationToken);
        if (session is null) return;

        await _chat.DeleteSessionAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> CreateSessionAsync(int? subjectId, CancellationToken cancellationToken = default)
    {
        var session = new ChatSession { SubjectId = subjectId, Title = "Phiên chat mới" };
        await _chat.AddSessionAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
            IsUser = m.Role == ChatMessageRole.User,
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
