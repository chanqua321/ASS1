namespace BusinessLogic.DTOs;

public class AdminTeachersPageDto
{
    public IReadOnlyList<SubjectOptionDto> AvailableSubjects { get; init; } = Array.Empty<SubjectOptionDto>();
    public IReadOnlyList<TeacherAssignmentDto> Teachers { get; init; } = Array.Empty<TeacherAssignmentDto>();
    public IReadOnlyList<UnassignedSubjectDto> UnassignedSubjects { get; init; } = Array.Empty<UnassignedSubjectDto>();
    public bool PreselectedSubjectAlreadyAssigned { get; init; }
}

public class SubjectOptionDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public class TeacherAssignmentDto
{
    public string Email { get; init; } = string.Empty;
    public IReadOnlyList<string> SubjectLabels { get; init; } = Array.Empty<string>();
}

public class UnassignedSubjectDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public class AssignTeacherResultDto
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public string? TeacherEmail { get; init; }
    public string? SubjectCode { get; init; }
    public bool CreatedTeacher { get; init; }
    public bool PromotedFromStudent { get; init; }

    public static AssignTeacherResultDto Ok(string teacherEmail, string subjectCode, bool createdTeacher, bool promotedFromStudent) =>
        new()
        {
            Success = true,
            TeacherEmail = teacherEmail,
            SubjectCode = subjectCode,
            CreatedTeacher = createdTeacher,
            PromotedFromStudent = promotedFromStudent
        };

    public static AssignTeacherResultDto Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

public class AssignTeacherFormValidationDto
{
    public bool RequiresPassword { get; init; }
}
