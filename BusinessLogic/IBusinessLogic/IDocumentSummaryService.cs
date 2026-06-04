namespace BusinessLogic.IBusinessLogic;

/// <summary>Tạo và lưu tóm tắt AI cho tài liệu đã được index.</summary>
public interface IDocumentSummaryService
{
    Task GenerateAndSaveAsync(int documentId, CancellationToken cancellationToken = default);
}
