using BusinessLogic.DTOs;
using BusinessLogic.IBusinessLogic;
using Model.IRepository;
using Model.IUnitOfWork;

namespace BusinessLogic.Logic;

public class TeacherAssignmentService : ITeacherAssignmentService
{
    private readonly ISubjectRepository _subjects;
    private readonly IUserRepository _users;
    private readonly IAuthService _auth;
    private readonly IUnitOfWork _unitOfWork;

    public TeacherAssignmentService(
        ISubjectRepository subjects,
        IUserRepository users,
        IAuthService auth,
        IUnitOfWork unitOfWork)
    {
        _subjects = subjects;
        _users = users;
        _auth = auth;
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminTeachersPageDto> GetAdminPageAsync(
        int? preselectSubjectId,
        CancellationToken cancellationToken = default)
    {
        var subjects = await _subjects.GetAllWithTeacherAsync(cancellationToken);
        var available = subjects.Where(s => !s.TeacherUserId.HasValue).ToList();

        var teachers = subjects
            .Where(s => s.TeacherUserId.HasValue && s.TeacherUser is not null)
            .Select(s => new { s.TeacherUser!.Email, Label = $"{s.Code} — {s.Name}" })
            .GroupBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g => new TeacherAssignmentDto
            {
                Email = g.Key,
                SubjectLabels = g.Select(x => x.Label).OrderBy(x => x).ToList()
            })
            .ToList();

        var preselectedAlreadyAssigned = preselectSubjectId is > 0
            && subjects.FirstOrDefault(s => s.Id == preselectSubjectId)?.TeacherUserId is not null;

        return new AdminTeachersPageDto
        {
            AvailableSubjects = available
                .Select(s => new SubjectOptionDto { Id = s.Id, Code = s.Code, Name = s.Name })
                .ToList(),
            Teachers = teachers,
            UnassignedSubjects = available
                .Select(s => new UnassignedSubjectDto { Id = s.Id, Code = s.Code, Name = s.Name })
                .ToList(),
            PreselectedSubjectAlreadyAssigned = preselectedAlreadyAssigned
        };
    }

    public async Task<AssignTeacherFormValidationDto> GetFormValidationHintsAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        email = (email ?? string.Empty).Trim();
        var existing = await _users.FindByEmailIgnoreCaseAsync(email, cancellationToken);
        var isExistingTeacher = existing?.Role == "Teacher";
        return new AssignTeacherFormValidationDto
        {
            RequiresPassword = !isExistingTeacher
        };
    }

    public async Task<AssignTeacherResultDto> AssignTeacherAsync(
        string email,
        string password,
        int subjectId,
        CancellationToken cancellationToken = default)
    {
        email = (email ?? string.Empty).Trim();

        var subject = await _subjects.GetByIdForUpdateAsync(subjectId, cancellationToken);
        if (subject is null)
            return AssignTeacherResultDto.Fail("Môn học không tồn tại.");

        if (subject.TeacherUserId.HasValue)
            return AssignTeacherResultDto.Fail($"Môn {subject.Code} đã có giáo viên.");

        var existingBefore = await _users.FindByEmailIgnoreCaseAsync(email, cancellationToken);
        var wasStudent = existingBefore?.Role == "Student";
        var wasNew = existingBefore is null;

        var (ok, err, user) = await _auth.PrepareTeacherUserAsync(email, password, cancellationToken);
        if (!ok || user is null)
            return AssignTeacherResultDto.Fail(err);

        subject.TeacherUserId = user.Id;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AssignTeacherResultDto.Ok(
            user.Email,
            subject.Code,
            createdTeacher: wasNew,
            promotedFromStudent: wasStudent);
    }
}
