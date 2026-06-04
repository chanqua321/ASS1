using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessLogic.IBusinessLogic;

namespace BusinessLogic.Logic;

/// <summary>
/// Placeholder embedding — thay bằng OpenAI/Azure AI khi tích hợp thật.
/// </summary>
public class MockEmbeddingService : IEmbeddingService
{
    public string ModelName => "mock-embedding-v1";
    private const int Dimensions = 64;

    public Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var vector = new float[Dimensions];
        for (var i = 0; i < Dimensions; i++)
            vector[i] = (hash[i % hash.Length] / 255f) * 2f - 1f;

        return Task.FromResult(vector);
    }

    public static string ToJson(float[] vector) => JsonSerializer.Serialize(vector);
}
