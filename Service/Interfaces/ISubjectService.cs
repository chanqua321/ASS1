using Model.Entities;

namespace Service.Interfaces;

public interface ISubjectService
{
    Task<IReadOnlyList<Subject>> GetAllWithChaptersAsync(CancellationToken cancellationToken = default);
    Task<Subject?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Subject> GetOrCreateByNameAsync(string name, string? code = null, CancellationToken cancellationToken = default);
    Task<Chapter> CreateChapterAsync(int subjectId, string title, CancellationToken cancellationToken = default);
}
