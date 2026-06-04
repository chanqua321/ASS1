using Microsoft.Extensions.Options;
using Model.Entities;
using Model.Enums;
using Model.IRepository;
using Model.IUnitOfWork;
using BusinessLogic.DTOs;
using BusinessLogic.Helpers;
using BusinessLogic.IBusinessLogic;
using BusinessLogic.Options;
using Model.Helpers;

namespace BusinessLogic.Logic;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly ISubjectRepository _subjects;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubjectService _subjectService;
    private readonly IChunkingService _chunking;
    private readonly IEmbeddingService _embedding;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly IDocumentSummaryService _documentSummary;
    private readonly string _uploadRoot;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".pptx", ".ppt"
    };

    public DocumentService(
        IDocumentRepository documents,
        ISubjectRepository subjects,
        IUnitOfWork unitOfWork,
        ISubjectService subjectService,
        IChunkingService chunking,
        IEmbeddingService embedding,
        IDocumentTextExtractor textExtractor,
        IDocumentSummaryService documentSummary,
        IOptions<DocumentStorageOptions> storage)
    {
        _documents = documents;
        _subjects = subjects;
        _unitOfWork = unitOfWork;
        _subjectService = subjectService;
        _chunking = chunking;
        _embedding = embedding;
        _textExtractor = textExtractor;
        _documentSummary = documentSummary;
        _uploadRoot = storage.Value.UploadPath;
    }

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        _documents.ExistsAsync(id, cancellationToken);

    public async Task<DocumentListItemDto> UploadAsync(DocumentUploadRequest request, CancellationToken cancellationToken = default)
    {
        // WF1 (Upload):
        // 1) Validate file + resolve Subject/Chapter
        // 2) Save file to disk (App_Data/uploads)
        // 3) Save Document entity (Pending)
        // 4) ProcessDocumentAsync: extract → chunk → embedding → save chunks → set Indexed/Failed
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ArgumentException("File is required.");

        var extension = Path.GetExtension(request.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Chỉ hỗ trợ PDF, DOCX hoặc slide (PPTX/PPT).");

        var (subjectId, chapterId) = await ResolveSubjectAndChapterAsync(request, cancellationToken);

        if (chapterId.HasValue &&
            await _documents.ExistsForSubjectChapterAsync(subjectId, chapterId.Value, cancellationToken))
        {
            throw new InvalidOperationException(
                "Chương này đã có tài liệu. Mỗi chương chỉ được upload một file — hãy dùng Index lại hoặc chọn chương khác.");
        }

        if (chapterId.HasValue)
            await EnsureNoSimilarChapterWithDocumentAsync(subjectId, chapterId.Value, cancellationToken);

        // Lưu file vật lý trước, sau đó DB chỉ lưu metadata + đường dẫn.
        Directory.CreateDirectory(_uploadRoot);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(_uploadRoot, storedFileName);

        await using (var stream = File.Create(physicalPath))
        {
            await request.FileStream.CopyToAsync(stream, cancellationToken);
        }

        var document = new Document
        {
            FileName = request.FileName,
            StoredFileName = storedFileName,
            ContentType = request.ContentType,
            FileType = MapFileType(extension),
            FileSizeBytes = request.FileSizeBytes,
            FilePath = physicalPath,
            SubjectId = subjectId,
            ChapterId = chapterId,
            UploadedByUserId = request.UploadedByUserId,
            Status = DocumentStatus.Pending
        };

        // SaveChanges lần 1: cần có document.Id để chạy ProcessDocumentAsync.
        await _documents.AddAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await ProcessDocumentAsync(document.Id, cancellationToken);

        return (await GetByIdAsync(document.Id, cancellationToken))!;
    }

    public async Task ProcessDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        // WF1 (Reindex / background processing):
        // - Load document + chunks hiện tại
        // - Set Processing (SaveChanges để UI thấy trạng thái)
        // - Xóa chunks cũ (nếu có)
        // - Extract text → chunk → embedding → add chunks
        // - Set Indexed/Failed + SaveChanges cuối
        var document = await _documents.GetByIdForProcessingAsync(documentId, cancellationToken);
        if (document is null)
            return;

        document.Status = DocumentStatus.Processing;
        document.ErrorMessage = null;
        document.Summary = null;
        document.SummaryGeneratedAt = null;
        // SaveChanges sớm: nếu user refresh trang, sẽ thấy trạng thái "Processing".
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            if (document.Chunks.Count > 0)
                await _documents.RemoveChunksAsync(document.Chunks, cancellationToken);

            // Text extractor: tùy theo file type (Pdf / Docx / Pptx).
            var text = await _textExtractor.ExtractTextAsync(document.FilePath, document.FileType, cancellationToken);
            var chunkTexts = _chunking.SplitIntoChunks(text);

            var index = 0;
            foreach (var chunkText in chunkTexts)
            {
                var chunk = new DocumentChunk
                {
                    DocumentId = document.Id,
                    ChunkIndex = index++,
                    Content = chunkText,
                    TokenCount = EstimateTokens(chunkText)
                };

                // Embedding: hiện đang là MockEmbeddingService (sau này có thể thay bằng model thật).
                var vector = await _embedding.CreateEmbeddingAsync(chunkText, cancellationToken);
                chunk.Embedding = new DocumentEmbedding
                {
                    ModelName = _embedding.ModelName,
                    VectorJson = MockEmbeddingService.ToJson(vector),
                    Dimensions = vector.Length
                };

                await _documents.AddChunkAsync(chunk, cancellationToken);
            }

            document.Status = DocumentStatus.Indexed;
            document.ProcessedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                await _documentSummary.GenerateAndSaveAsync(document.Id, cancellationToken);
            }
            catch
            {
                // Tóm tắt AI là bổ trợ — không làm fail quá trình index.
            }
        }
        catch (Exception ex)
        {
            document.Status = DocumentStatus.Failed;
            document.ErrorMessage = ex.Message;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }
    }

    public async Task<IReadOnlyList<DocumentListItemDto>> GetProcessedDocumentsAsync(
        int? subjectId = null,
        int? teacherUserId = null,
        int? viewerUserId = null,
        bool viewerIsAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _documents.GetProcessedListAsync(subjectId, teacherUserId, cancellationToken);
        return items.Select(d => MapToListItem(d, viewerUserId, viewerIsAdmin)).ToList();
    }

    public Task<bool> CanDeleteAsync(
        int documentId,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken = default) =>
        CanUserDeleteDocumentAsync(documentId, userId, isAdmin, cancellationToken);

    public async Task<(bool Success, string ErrorMessage)> DeleteAsync(
        int documentId,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!await CanUserDeleteDocumentAsync(documentId, userId, isAdmin, cancellationToken))
            return (false, "Chỉ người đã upload hoặc Admin mới được xóa tài liệu này.");

        var document = await _documents.GetByIdForDeleteAsync(documentId, cancellationToken);
        if (document is null)
            return (false, "Tài liệu không tồn tại.");

        var filePath = document.FilePath;

        await _documents.DeleteCitationsForDocumentAsync(documentId, cancellationToken);
        _documents.Remove(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            try { File.Delete(filePath); }
            catch
            {
                // DB đã xóa; file disk có thể xóa tay sau.
            }
        }

        return (true, "");
    }

    private async Task<bool> CanUserDeleteDocumentAsync(
        int documentId,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        if (isAdmin)
            return await _documents.ExistsAsync(documentId, cancellationToken);

        var uploadedBy = await _documents.GetUploadedByUserIdAsync(documentId, cancellationToken);
        return uploadedBy.HasValue && uploadedBy.Value == userId;
    }

    public Task<bool> TeacherCanAccessAsync(
        int documentId,
        int teacherUserId,
        CancellationToken cancellationToken = default) =>
        _documents.IsInTeacherSubjectAsync(documentId, teacherUserId, cancellationToken);

    public async Task<DocumentListItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var doc = await _documents.GetByIdWithDetailsAsync(id, cancellationToken);
        return doc is null ? null : MapToListItem(doc);
    }

    public async Task<DocumentDetailsDto?> GetDetailsAsync(
        int id,
        int? viewerUserId = null,
        bool viewerIsAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var doc = await _documents.GetByIdWithDetailsAsync(id, cancellationToken);
        if (doc is null) return null;

        var baseDto = MapToListItem(doc, viewerUserId, viewerIsAdmin);
        return new DocumentDetailsDto
        {
            Id = baseDto.Id,
            FileName = baseDto.FileName,
            FileType = baseDto.FileType,
            Status = baseDto.Status,
            SubjectCode = baseDto.SubjectCode,
            SubjectName = baseDto.SubjectName,
            ChapterTitle = baseDto.ChapterTitle,
            ChunkCount = baseDto.ChunkCount,
            UploadedAt = baseDto.UploadedAt,
            ProcessedAt = baseDto.ProcessedAt,
            StatusLabel = baseDto.StatusLabel,
            StatusBadgeClass = baseDto.StatusBadgeClass,
            StatusIcon = baseDto.StatusIcon,
            IsIndexed = baseDto.IsIndexed,
            FileTypeLabel = baseDto.FileTypeLabel,
            Summary = baseDto.Summary,
            SummaryGeneratedAt = baseDto.SummaryGeneratedAt,
            SummaryPreview = baseDto.SummaryPreview,
            HasSearchableText = baseDto.HasSearchableText,
            UploadedByUserId = baseDto.UploadedByUserId,
            CanDelete = baseDto.CanDelete
        };
    }

    public async Task<DocumentDownloadDto?> GetDownloadAsync(int id, CancellationToken cancellationToken = default)
    {
        var doc = await _documents.GetByIdForDownloadAsync(id, cancellationToken);
        if (doc is null || !File.Exists(doc.FilePath))
            return null;

        var stream = new FileStream(doc.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new DocumentDownloadDto
        {
            FileStream = stream,
            FileName = doc.FileName,
            ContentType = string.IsNullOrWhiteSpace(doc.ContentType)
                ? "application/octet-stream"
                : doc.ContentType
        };
    }

    private async Task<(int subjectId, int? chapterId)> ResolveSubjectAndChapterAsync(
        DocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        int subjectId;

        if (!string.IsNullOrWhiteSpace(request.NewSubjectName))
        {
            subjectId = await _subjectService.GetOrCreateByNameAsync(
                request.NewSubjectName,
                request.NewSubjectCode,
                cancellationToken);
        }
        else if (request.SubjectId is > 0)
        {
            if (!await _subjects.ExistsAsync(request.SubjectId.Value, cancellationToken))
                throw new InvalidOperationException("Môn học không tồn tại.");
            subjectId = request.SubjectId.Value;
        }
        else
        {
            throw new InvalidOperationException("Vui lòng chọn môn học hoặc nhập tên môn mới.");
        }

        int? chapterId = null;

        if (!string.IsNullOrWhiteSpace(request.NewChapterTitle))
        {
            var similar = await _subjects.FindChapterBySimilarTitleAsync(
                subjectId,
                request.NewChapterTitle,
                cancellationToken);

            chapterId = similar?.Id ?? await _subjectService.CreateChapterAsync(
                subjectId,
                request.NewChapterTitle,
                cancellationToken);
        }
        else if (request.ChapterId.HasValue)
        {
            if (!await _subjects.ChapterBelongsToSubjectAsync(request.ChapterId.Value, subjectId, cancellationToken))
                throw new InvalidOperationException("Chương không thuộc môn học đã chọn.");
            chapterId = request.ChapterId;
        }

        return (subjectId, chapterId);
    }

    private async Task EnsureNoSimilarChapterWithDocumentAsync(
        int subjectId,
        int chapterId,
        CancellationToken cancellationToken)
    {
        var incomingTitle = await _documents.GetChapterTitleAsync(chapterId, cancellationToken);
        if (string.IsNullOrWhiteSpace(incomingTitle))
            return;

        var rows = await _documents.GetChapterDocumentRowsAsync(subjectId, cancellationToken);
        foreach (var row in rows)
        {
            if (row.ChapterId == chapterId)
                continue;

            if (!ChapterTitleHelper.AreSimilar(incomingTitle, row.ChapterTitle))
                continue;

            throw new InvalidOperationException(
                $"Môn này đã có tài liệu cho chương tương tự \"{row.ChapterTitle}\". " +
                "Không upload thêm bản trùng — hãy Index lại file cũ hoặc chọn tên chương khác (ví dụ Chương 2).");
        }
    }

    private static DocumentListItemDto MapToListItem(
        Document d,
        int? viewerUserId = null,
        bool viewerIsAdmin = false)
    {
        var (badge, icon, label) = DocumentDisplayHelper.GetStatusDisplay(d.Status);
        var canDelete = viewerIsAdmin ||
                        (viewerUserId.HasValue &&
                         d.UploadedByUserId.HasValue &&
                         d.UploadedByUserId.Value == viewerUserId.Value);
        return new DocumentListItemDto
        {
            Id = d.Id,
            FileName = d.FileName,
            FileType = d.FileType,
            FileTypeLabel = d.FileType.ToString(),
            Status = d.Status,
            StatusLabel = label,
            StatusBadgeClass = badge,
            StatusIcon = icon,
            IsIndexed = DocumentDisplayHelper.IsIndexed(d.Status),
            SubjectCode = d.Subject.Code,
            SubjectName = d.Subject.Name,
            ChapterTitle = d.Chapter?.Title,
            ChunkCount = d.Chunks.Count,
            UploadedAt = d.UploadedAt,
            ProcessedAt = d.ProcessedAt,
            Summary = d.Summary,
            SummaryGeneratedAt = d.SummaryGeneratedAt,
            SummaryPreview = BuildSummaryPreview(d.Summary),
            HasSearchableText = RagAnswerSanitizer.DocumentHasSearchableText(d.Chunks.Select(c => c.Content)),
            UploadedByUserId = d.UploadedByUserId,
            CanDelete = canDelete
        };
    }

    private static string? BuildSummaryPreview(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return null;
        const int maxLen = 100;
        var trimmed = summary.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return trimmed.Length <= maxLen ? trimmed : trimmed[..maxLen] + "…";
    }

    private static DocumentFileType MapFileType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => DocumentFileType.Pdf,
        ".docx" => DocumentFileType.Docx,
        ".pptx" or ".ppt" => DocumentFileType.Pptx,
        _ => DocumentFileType.Unknown
    };

    private static int EstimateTokens(string text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);
}
