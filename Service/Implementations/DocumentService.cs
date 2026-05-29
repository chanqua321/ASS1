using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Entities;
using Model.Enums;
using Service.DTOs;
using Service.Interfaces;

namespace Service.Implementations;

public class DocumentService : IDocumentService
{
    private readonly AppDbContext _db;
    private readonly ISubjectService _subjectService;
    private readonly IChunkingService _chunking;
    private readonly IEmbeddingService _embedding;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly string _uploadRoot;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".pptx", ".ppt"
    };

    public DocumentService(
        AppDbContext db,
        ISubjectService subjectService,
        IChunkingService chunking,
        IEmbeddingService embedding,
        IDocumentTextExtractor textExtractor,
        string uploadRoot)
    {
        _db = db;
        _subjectService = subjectService;
        _chunking = chunking;
        _embedding = embedding;
        _textExtractor = textExtractor;
        _uploadRoot = uploadRoot;
    }

    public async Task<DocumentListItemDto> UploadAsync(DocumentUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ArgumentException("File is required.");

        var extension = Path.GetExtension(request.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Chỉ hỗ trợ PDF, DOCX hoặc slide (PPTX/PPT).");

        var (subjectId, chapterId) = await ResolveSubjectAndChapterAsync(request, cancellationToken);

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

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        await ProcessDocumentAsync(document.Id, cancellationToken);

        return (await GetByIdAsync(document.Id, cancellationToken))!;
    }

    public async Task ProcessDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document is null)
            return;

        document.Status = DocumentStatus.Processing;
        document.ErrorMessage = null;
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            if (document.Chunks.Count > 0)
            {
                _db.DocumentChunks.RemoveRange(document.Chunks);
                await _db.SaveChangesAsync(cancellationToken);
            }

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

                var vector = await _embedding.CreateEmbeddingAsync(chunkText, cancellationToken);
                chunk.Embedding = new DocumentEmbedding
                {
                    ModelName = _embedding.ModelName,
                    VectorJson = MockEmbeddingService.ToJson(vector),
                    Dimensions = vector.Length
                };

                _db.DocumentChunks.Add(chunk);
            }

            document.Status = DocumentStatus.Indexed;
            document.ProcessedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            document.Status = DocumentStatus.Failed;
            document.ErrorMessage = ex.Message;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentListItemDto>> GetProcessedDocumentsAsync(
        int? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Documents
            .AsNoTracking()
            .Include(d => d.Subject)
            .Include(d => d.Chapter)
            .Include(d => d.Chunks)
            .Where(d => d.Status == DocumentStatus.Indexed || d.Status == DocumentStatus.Processing || d.Status == DocumentStatus.Failed);

        if (subjectId.HasValue)
            query = query.Where(d => d.SubjectId == subjectId);

        var items = await query
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        return items.Select(MapToListItem).ToList();
    }

    public async Task<DocumentListItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Subject)
            .Include(d => d.Chapter)
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return doc is null ? null : MapToListItem(doc);
    }

    public async Task<DocumentDownloadDto?> GetDownloadAsync(int id, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

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
            var subject = await _subjectService.GetOrCreateByNameAsync(
                request.NewSubjectName,
                request.NewSubjectCode,
                cancellationToken);
            subjectId = subject.Id;
        }
        else if (request.SubjectId is > 0)
        {
            var exists = await _db.Subjects.AnyAsync(s => s.Id == request.SubjectId, cancellationToken);
            if (!exists)
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
            var chapter = await _subjectService.CreateChapterAsync(
                subjectId,
                request.NewChapterTitle,
                cancellationToken);
            chapterId = chapter.Id;
        }
        else if (request.ChapterId.HasValue)
        {
            var chapterValid = await _db.Chapters.AnyAsync(
                c => c.Id == request.ChapterId && c.SubjectId == subjectId,
                cancellationToken);
            if (!chapterValid)
                throw new InvalidOperationException("Chương không thuộc môn học đã chọn.");
            chapterId = request.ChapterId;
        }

        return (subjectId, chapterId);
    }

    private static DocumentListItemDto MapToListItem(Document d) => new()
    {
        Id = d.Id,
        FileName = d.FileName,
        FileType = d.FileType,
        Status = d.Status,
        SubjectName = d.Subject.Name,
        ChapterTitle = d.Chapter?.Title,
        ChunkCount = d.Chunks.Count,
        UploadedAt = d.UploadedAt,
        ProcessedAt = d.ProcessedAt
    };

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
