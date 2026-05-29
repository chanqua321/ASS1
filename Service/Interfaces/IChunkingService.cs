namespace Service.Interfaces;

public interface IChunkingService
{
    IReadOnlyList<string> SplitIntoChunks(string text, int maxChunkSize = 800, int overlap = 100);
}
