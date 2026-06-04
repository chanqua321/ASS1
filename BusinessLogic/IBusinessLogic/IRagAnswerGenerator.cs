using BusinessLogic.DTOs;

namespace BusinessLogic.IBusinessLogic;

public interface IRagAnswerGenerator
{
    Task<(string Answer, bool FromDocuments)> GenerateAsync(
        string question,
        IReadOnlyList<RetrievedChunkDto> chunks,
        IReadOnlyList<string> recentConversation,
        bool includeCitationHints = false,
        bool isSummaryQuestion = false,
        CancellationToken cancellationToken = default);
}
