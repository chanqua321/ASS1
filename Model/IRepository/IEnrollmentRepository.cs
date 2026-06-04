using Model.Entities;

namespace Model.IRepository;

public interface IEnrollmentRepository
{
    Task<bool> SubjectHasIndexedDocumentsAsync(int subjectId, CancellationToken cancellationToken = default);
    Task<Subject?> GetSubjectByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> EnrollmentExistsAsync(int subjectId, string email, CancellationToken cancellationToken = default);
    Task AddEnrollmentAsync(SubjectEnrollment enrollment, CancellationToken cancellationToken = default);
    Task<bool> IsEnrolledAsync(int subjectId, string email, CancellationToken cancellationToken = default);
}
