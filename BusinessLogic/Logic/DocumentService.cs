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
        CancellationToken cancellationToken = default)
    {
        var items = await _documents.GetProcessedListAsync(subjectId, teacherUserId, cancellationToken);
        var deduped = DeduplicateByChapter(items);
        return deduped.Select(MapToListItem).ToList();
    }

    public Task<bool> TeacherCanAccessAsync(
        int documentId,
        int teacherUserId,
        CancellationToken cancellationToken = default) =>
        _documents.IsInTeacherSubjectAsync(documentId, teacherUserId, cancellationToken);

    private static List<Document> DeduplicateByChapter(List<Document> items)
    {
        var withChapter = items.Where(d => d.ChapterId.HasValue).ToList();
        var withoutChapter = items.Where(d => !d.ChapterId.HasValue).ToList();

        var latestPerChapter = withChapter
            .GroupBy(d => new { d.SubjectId, d.ChapterId })
            .Select(g => g.OrderByDescending(x => x.UploadedAt).First());

        return latestPerChapter
            .Concat(withoutChapter)
            .OrderByDescending(d => d.UploadedAt)
            .ToList();
    }

    public async Task<DocumentListItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var doc = await _documents.GetByIdWithDetailsAsync(id, cancellationToken);
        return doc is null ? null : MapToListItem(doc);
    }

    public async Task<DocumentDetailsDto?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        var doc = await _documents.GetByIdWithDetailsAsync(id, cancellationToken);
        if (doc is null) return null;

        var baseDto = MapToListItem(doc);
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
            SummaryPreview = baseDto.SummaryPreview
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
            chapterId = await _subjectService.CreateChapterAsync(
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

    private static DocumentListItemDto MapToListItem(Document d)
    {
        var (badge, icon, label) = DocumentDisplayHelper.GetStatusDisplay(d.Status);
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
            SummaryPreview = BuildSummaryPreview(d.Summary)
        };
    }

    private static string? BuildSummaryPreview(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return null;
        const int maxLen = 160;
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
