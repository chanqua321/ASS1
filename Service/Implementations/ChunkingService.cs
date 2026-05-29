using Service.Interfaces;

namespace Service.Implementations;

public class ChunkingService : IChunkingService
{
    public IReadOnlyList<string> SplitIntoChunks(string text, int maxChunkSize = 800, int overlap = 100)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var normalized = text.Replace("\r\n", "\n").Trim();
        var chunks = new List<string>();
        var start = 0;

        while (start < normalized.Length)
        {
            var length = Math.Min(maxChunkSize, normalized.Length - start);
            var slice = normalized.Substring(start, length).Trim();
            if (!string.IsNullOrEmpty(slice))
                chunks.Add(slice);

            if (start + length >= normalized.Length)
                break;

            start += Math.Max(1, maxChunkSize - overlap);
        }

        return chunks;
    }
}
