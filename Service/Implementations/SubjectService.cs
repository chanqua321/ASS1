using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Model.Data;
using Model.Entities;
using Service.Interfaces;

namespace Service.Implementations;

public partial class SubjectService : ISubjectService
{
    private readonly AppDbContext _db;

    public SubjectService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Subject>> GetAllWithChaptersAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Subjects
            .Include(s => s.Chapters.OrderBy(c => c.OrderNumber))
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Subject?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Subjects
            .Include(s => s.Chapters)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Subject> GetOrCreateByNameAsync(string name, string? code = null, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new ArgumentException("Tên môn học không được để trống.");

        var existing = await _db.Subjects
            .FirstOrDefaultAsync(s => s.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (existing is not null)
            return existing;

        var subjectCode = string.IsNullOrWhiteSpace(code)
            ? await GenerateUniqueCodeAsync(trimmedName, cancellationToken)
            : code.Trim().ToUpperInvariant();

        if (await _db.Subjects.AnyAsync(s => s.Code == subjectCode, cancellationToken))
            throw new InvalidOperationException($"Mã môn '{subjectCode}' đã tồn tại. Vui lòng nhập mã khác.");

        var subject = new Subject
        {
            Name = trimmedName,
            Code = subjectCode
        };

        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync(cancellationToken);
        return subject;
    }

    public async Task<Chapter> CreateChapterAsync(int subjectId, string title, CancellationToken cancellationToken = default)
    {
        var trimmedTitle = title.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTitle))
            throw new ArgumentException("Tên chương không được để trống.");

        var subjectExists = await _db.Subjects.AnyAsync(s => s.Id == subjectId, cancellationToken);
        if (!subjectExists)
            throw new InvalidOperationException("Môn học không tồn tại.");

        var maxOrder = await _db.Chapters
            .Where(c => c.SubjectId == subjectId)
            .Select(c => (int?)c.OrderNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var chapter = new Chapter
        {
            SubjectId = subjectId,
            Title = trimmedTitle,
            OrderNumber = maxOrder + 1
        };

        _db.Chapters.Add(chapter);
        await _db.SaveChangesAsync(cancellationToken);
        return chapter;
    }

    private async Task<string> GenerateUniqueCodeAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = RemoveDiacritics(name).ToUpperInvariant();
        var slug = NonAlphaNumeric().Replace(normalized, "");
        var baseCode = slug.Length > 12 ? slug[..12] : slug;
        if (string.IsNullOrEmpty(baseCode))
            baseCode = "MON";

        var code = baseCode;
        var suffix = 1;
        while (await _db.Subjects.AnyAsync(s => s.Code == code, cancellationToken))
            code = $"{baseCode}{suffix++}";

        return code;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"[^A-Z0-9]", RegexOptions.None)]
    private static partial Regex NonAlphaNumeric();
}
