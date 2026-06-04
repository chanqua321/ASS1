using BusinessLogic.DTOs;

namespace BusinessLogic.IBusinessLogic;

public interface ITeacherAssignmentService
{
    Task<AdminTeachersPageDto> GetAdminPageAsync(int? preselectSubjectId, CancellationToken cancellationToken = default);

    Task<AssignTeacherFormValidationDto> GetFormValidationHintsAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<AssignTeacherResultDto> AssignTeacherAsync(
        string email,
        string password,
        int subjectId,
        CancellationToken cancellationToken = default);
}
