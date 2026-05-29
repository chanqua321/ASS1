using Service.DTOs;

namespace Service.Interfaces;

public interface IEnrollmentService
{
    Task<bool> SubjectHasIndexedDocumentsAsync(int subjectId, CancellationToken cancellationToken = default);

    Task<SubjectEnrollmentResultDto> EnrollAsync(
        SubjectEnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> IsEnrolledAsync(int subjectId, string email, CancellationToken cancellationToken = default);
}
