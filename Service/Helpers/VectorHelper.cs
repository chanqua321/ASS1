using System.Text.Json;

namespace Service.Helpers;

public static class VectorHelper
{
    public static float[] ParseVector(string vectorJson)
    {
        if (string.IsNullOrWhiteSpace(vectorJson))
            return Array.Empty<float>();

        try
        {
            return JsonSerializer.Deserialize<float[]>(vectorJson) ?? Array.Empty<float>();
        }
        catch
        {
            return Array.Empty<float>();
        }
    }

    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0;

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
