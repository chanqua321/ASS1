using BusinessLogic.DTOs;

namespace BusinessLogic.IBusinessLogic;

public interface IEnrollmentService
{
    Task<bool> SubjectHasIndexedDocumentsAsync(int subjectId, CancellationToken cancellationToken = default);

    Task<SubjectEnrollmentResultDto> EnrollAsync(
        SubjectEnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> IsEnrolledAsync(int subjectId, string email, CancellationToken cancellationToken = default);
}
