namespace Service.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    string ModelName { get; }
}
