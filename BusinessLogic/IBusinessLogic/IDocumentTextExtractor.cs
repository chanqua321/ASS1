namespace BusinessLogic.IBusinessLogic;

public interface IDocumentTextExtractor
{
    Task<string> ExtractTextAsync(string filePath, Model.Enums.DocumentFileType fileType, CancellationToken cancellationToken = default);
}
