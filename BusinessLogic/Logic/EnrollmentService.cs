using Model.Entities;
using Model.IRepository;
using Model.IUnitOfWork;
using BusinessLogic.DTOs;
using BusinessLogic.IBusinessLogic;

namespace BusinessLogic.Logic;

public class EnrollmentService : IEnrollmentService
{
    private readonly IEnrollmentRepository _enrollments;
    private readonly IUnitOfWork _unitOfWork;

    public EnrollmentService(IEnrollmentRepository enrollments, IUnitOfWork unitOfWork)
    {
        _enrollments = enrollments;
        _unitOfWork = unitOfWork;
    }

    public Task<bool> SubjectHasIndexedDocumentsAsync(int subjectId, CancellationToken cancellationToken = default) =>
        _enrollments.SubjectHasIndexedDocumentsAsync(subjectId, cancellationToken);

    public async Task<SubjectEnrollmentResultDto> EnrollAsync(
        SubjectEnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.FullName?.Trim() ?? "";
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";

        if (name.Length < 2)
            return Fail("Họ tên phải có ít nhất 2 ký tự.");

        if (!email.Contains('@') || email.Length < 5)
            return Fail("Email không hợp lệ.");

        var subject = await _enrollments.GetSubjectByIdAsync(request.SubjectId, cancellationToken);
        if (subject is null)
            return Fail("Môn học không tồn tại.");

        if (!await SubjectHasIndexedDocumentsAsync(request.SubjectId, cancellationToken))
        {
            return Fail(
                $"Môn {subject.Code} chưa có tài liệu đã index. Vui lòng upload tài liệu trước khi đăng ký.");
        }

        if (await _enrollments.EnrollmentExistsAsync(request.SubjectId, email, cancellationToken))
        {
            return new SubjectEnrollmentResultDto
            {
                Success = true,
                SubjectName = subject.Name,
                Message = $"Bạn đã đăng ký môn {subject.Code} — {subject.Name} trước đó."
            };
        }

        await _enrollments.AddEnrollmentAsync(new SubjectEnrollment
        {
            SubjectId = request.SubjectId,
            FullName = name,
            Email = email
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubjectEnrollmentResultDto
        {
            Success = true,
            SubjectName = subject.Name,
            Message = $"Đăng ký thành công môn {subject.Code} — {subject.Name}. Bạn có thể chat hỏi đáp tài liệu môn này."
        };
    }

    public Task<bool> IsEnrolledAsync(int subjectId, string email, CancellationToken cancellationToken = default) =>
        _enrollments.IsEnrolledAsync(subjectId, email, cancellationToken);

    private static SubjectEnrollmentResultDto Fail(string message) =>
        new() { Success = false, Message = message };
}
