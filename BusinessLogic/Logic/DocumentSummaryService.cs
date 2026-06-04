using BusinessLogic.DTOs;
using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;
using BusinessLogic.Options;
using Microsoft.Extensions.Options;
using Model.IRepository;
using Model.IUnitOfWork;

namespace BusinessLogic.Logic;

public class DocumentSummaryService : IDocumentSummaryService
{
    private readonly IDocumentRepository _documents;
    private readonly IChunkRepository _chunks;
    private readonly IRagAnswerGenerator _rag;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RagChatOptions _ragOptions;

    public DocumentSummaryService(
        IDocumentRepository documents,
        IChunkRepository chunks,
        IRagAnswerGenerator rag,
        IUnitOfWork unitOfWork,
        IOptions<RagChatOptions> ragOptions)
    {
        _documents = documents;
        _chunks = chunks;
        _rag = rag;
        _unitOfWork = unitOfWork;
        _ragOptions = ragOptions.Value;
    }

    public async Task GenerateAndSaveAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await _documents.GetByIdForProcessingAsync(documentId, cancellationToken);
        if (document is null || !DocumentDisplayHelper.IsIndexed(document.Status))
            return;

        var chunkEntities = await _chunks.GetByDocumentIdOrderedAsync(documentId, cancellationToken);
        if (chunkEntities.Count == 0)
            return;

        var chunks = chunkEntities.Select(c => new RetrievedChunkDto
        {
            ChunkId = c.Id,
            DocumentId = c.DocumentId,
            FileName = document.FileName,
            ChapterTitle = document.Chapter?.Title,
            SubjectName = document.Subject?.Name ?? "",
            SubjectCode = document.Subject?.Code ?? "",
            Content = c.Content,
            Score = 1.0
        }).ToList();

        var selected = RagChunkSelector.SelectForSummary(chunks, _ragOptions);
        if (selected.Count == 0)
            selected = chunks;

        if (RagAnswerSanitizer.AreChunksOnlyPlaceholders(selected))
        {
            document.Summary = RagAnswerSanitizer.GetNoExtractableTextSummary(document.FileType, document.FileName);
            document.SummaryGeneratedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var question =
            $"Tóm tắt toàn bộ nội dung tài liệu \"{document.FileName}\" " +
            $"thuộc môn {document.Subject?.Name ?? "học tập"}.";

        var (summary, _) = await _rag.GenerateAsync(
            question,
            selected,
            Array.Empty<ChatHistoryTurnDto>(),
            includeCitationHints: false,
            isSummaryQuestion: true,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(summary))
            return;

        document.Summary = summary.Trim();
        document.SummaryGeneratedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
