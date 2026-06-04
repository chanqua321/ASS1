using BusinessLogic.DTOs;

namespace BusinessLogic.IBusinessLogic;

public interface IRetrievalService
{
    Task<IReadOnlyList<RetrievedChunkDto>> RetrieveAsync(
        string query,
        int? subjectId = null,
        int topK = 5,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetrievedChunkDto>> RetrieveForSummaryAsync(
        string query,
        int? subjectId = null,
        int maxChunks = 12,
        CancellationToken cancellationToken = default);
}
