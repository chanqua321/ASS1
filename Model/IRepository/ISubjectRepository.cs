using Model.Entities;

namespace Model.IRepository;

public interface ISubjectRepository
{
    Task<List<Subject>> GetAllWithChaptersAsync(CancellationToken cancellationToken = default);
    Task<Subject?> GetByIdWithChaptersAsync(int id, CancellationToken cancellationToken = default);
    Task<Subject?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task AddAsync(Subject subject, CancellationToken cancellationToken = default);
    Task<int?> GetMaxChapterOrderAsync(int subjectId, CancellationToken cancellationToken = default);
    Task AddChapterAsync(Chapter chapter, CancellationToken cancellationToken = default);
    Task<bool> ChapterBelongsToSubjectAsync(int chapterId, int subjectId, CancellationToken cancellationToken = default);
    Task<Chapter?> FindChapterBySimilarTitleAsync(
        int subjectId,
        string title,
        CancellationToken cancellationToken = default);
    Task<List<Subject>> GetAllWithTeacherAsync(CancellationToken cancellationToken = default);
    Task<Subject?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken = default);
}
