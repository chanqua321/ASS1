using Model.IRepository;
using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Entities;
using Model.Enums;

namespace Model.Repository;

public class EnrollmentRepository(AppDbContext db) : IEnrollmentRepository
{
    public Task<bool> SubjectHasIndexedDocumentsAsync(int subjectId, CancellationToken cancellationToken = default) =>
        db.Documents.AnyAsync(
            d => d.SubjectId == subjectId && d.Status == DocumentStatus.Indexed,
            cancellationToken);

    public Task<Subject?> GetSubjectByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Subjects.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> EnrollmentExistsAsync(int subjectId, string email, CancellationToken cancellationToken = default) =>
        db.SubjectEnrollments.AnyAsync(
            e => e.SubjectId == subjectId && e.Email == email,
            cancellationToken);

    public async Task AddEnrollmentAsync(SubjectEnrollment enrollment, CancellationToken cancellationToken = default) =>
        await db.SubjectEnrollments.AddAsync(enrollment, cancellationToken);

    public Task<bool> IsEnrolledAsync(int subjectId, string email, CancellationToken cancellationToken = default) =>
        db.SubjectEnrollments.AnyAsync(
            e => e.SubjectId == subjectId && e.Email == email,
            cancellationToken);
}
