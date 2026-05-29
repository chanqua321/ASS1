using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Entities;
using Model.Enums;
using Service.DTOs;
using Service.Interfaces;

namespace Service.Implementations;

public class EnrollmentService : IEnrollmentService
{
    private readonly AppDbContext _db;

    public EnrollmentService(AppDbContext db) => _db = db;

    public Task<bool> SubjectHasIndexedDocumentsAsync(int subjectId, CancellationToken cancellationToken = default) =>
        _db.Documents.AnyAsync(
            d => d.SubjectId == subjectId && d.Status == DocumentStatus.Indexed,
            cancellationToken);

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

        var subject = await _db.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SubjectId, cancellationToken);

        if (subject is null)
            return Fail("Môn học không tồn tại.");

        if (!await SubjectHasIndexedDocumentsAsync(request.SubjectId, cancellationToken))
        {
            return Fail(
                $"Môn {subject.Code} chưa có tài liệu đã index. Vui lòng upload tài liệu trước khi đăng ký.");
        }

        var exists = await _db.SubjectEnrollments.AnyAsync(
            e => e.SubjectId == request.SubjectId && e.Email == email,
            cancellationToken);

        if (exists)
        {
            return new SubjectEnrollmentResultDto
            {
                Success = true,
                SubjectName = subject.Name,
                Message = $"Bạn đã đăng ký môn {subject.Code} — {subject.Name} trước đó."
            };
        }

        _db.SubjectEnrollments.Add(new SubjectEnrollment
        {
            SubjectId = request.SubjectId,
            FullName = name,
            Email = email
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new SubjectEnrollmentResultDto
        {
            Success = true,
            SubjectName = subject.Name,
            Message = $"Đăng ký thành công môn {subject.Code} — {subject.Name}. Bạn có thể chat hỏi đáp tài liệu môn này."
        };
    }

    public Task<bool> IsEnrolledAsync(int subjectId, string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.SubjectEnrollments.AnyAsync(
            e => e.SubjectId == subjectId && e.Email == normalized,
            cancellationToken);
    }

    private static SubjectEnrollmentResultDto Fail(string message) =>
        new() { Success = false, Message = message };
}
