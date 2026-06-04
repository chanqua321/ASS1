using BusinessLogic.DTOs;

namespace BusinessLogic.IBusinessLogic;

public interface ISubjectService
{
    Task<IReadOnlyList<SubjectListItemDto>> GetAllWithChaptersAsync(CancellationToken cancellationToken = default);
    Task<SubjectListItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> GetOrCreateByNameAsync(string name, string? code = null, CancellationToken cancellationToken = default);
    Task<int> CreateChapterAsync(int subjectId, string title, CancellationToken cancellationToken = default);
    Task<(bool Success, string ErrorMessage, int SubjectId)> CreateSubjectAsync(
        string code,
        string name,
        string? description = null,
        CancellationToken cancellationToken = default);
}
