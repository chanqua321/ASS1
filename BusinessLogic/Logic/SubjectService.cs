using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Model.Entities;
using Model.IRepository;
using Model.IUnitOfWork;
using BusinessLogic.DTOs;
using BusinessLogic.IBusinessLogic;

namespace BusinessLogic.Logic;

public partial class SubjectService : ISubjectService
{
    private readonly ISubjectRepository _subjects;
    private readonly IUnitOfWork _unitOfWork;

    public SubjectService(ISubjectRepository subjects, IUnitOfWork unitOfWork)
    {
        _subjects = subjects;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SubjectListItemDto>> GetAllWithChaptersAsync(CancellationToken cancellationToken = default)
    {
        var subjects = await _subjects.GetAllWithChaptersAsync(cancellationToken);
        return subjects.Select(MapSubject).ToList();
    }

    public async Task<SubjectListItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var subject = await _subjects.GetByIdWithChaptersAsync(id, cancellationToken);
        return subject is null ? null : MapSubject(subject);
    }

    public async Task<int> GetOrCreateByNameAsync(string name, string? code = null, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new ArgumentException("Tên môn học không được để trống.");

        var existing = await _subjects.FindByNameAsync(trimmedName, cancellationToken);
        if (existing is not null)
            return existing.Id;

        var subjectCode = string.IsNullOrWhiteSpace(code)
            ? await GenerateUniqueCodeAsync(trimmedName, cancellationToken)
            : code.Trim().ToUpperInvariant();

        if (await _subjects.CodeExistsAsync(subjectCode, cancellationToken))
            throw new InvalidOperationException($"Mã môn '{subjectCode}' đã tồn tại. Vui lòng nhập mã khác.");

        var subject = new Subject { Name = trimmedName, Code = subjectCode };
        await _subjects.AddAsync(subject, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return subject.Id;
    }

    public async Task<(bool Success, string ErrorMessage, int SubjectId)> CreateSubjectAsync(
        string code,
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        code = (code ?? string.Empty).Trim().ToUpperInvariant();
        name = (name ?? string.Empty).Trim();
        description = (description ?? string.Empty).Trim();

        if (code.Length == 0)
            return (false, "Thiếu mã môn (Code).", 0);
        if (name.Length == 0)
            return (false, "Thiếu tên môn.", 0);
        if (await _subjects.CodeExistsAsync(code, cancellationToken))
            return (false, $"Mã môn '{code}' đã tồn tại.", 0);

        var subject = new Subject
        {
            Code = code,
            Name = name,
            Description = string.IsNullOrEmpty(description) ? null : description
        };
        await _subjects.AddAsync(subject, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (true, "", subject.Id);
    }

    public async Task<int> CreateChapterAsync(int subjectId, string title, CancellationToken cancellationToken = default)
    {
        var trimmedTitle = title.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTitle))
            throw new ArgumentException("Tên chương không được để trống.");

        if (!await _subjects.ExistsAsync(subjectId, cancellationToken))
            throw new InvalidOperationException("Môn học không tồn tại.");

        var maxOrder = await _subjects.GetMaxChapterOrderAsync(subjectId, cancellationToken) ?? 0;

        var chapter = new Chapter
        {
            SubjectId = subjectId,
            Title = trimmedTitle,
            OrderNumber = maxOrder + 1
        };

        await _subjects.AddChapterAsync(chapter, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return chapter.Id;
    }

    private static SubjectListItemDto MapSubject(Subject subject) => new()
    {
        Id = subject.Id,
        Code = subject.Code,
        Name = subject.Name,
        TeacherUserId = subject.TeacherUserId,
        TeacherEmail = subject.TeacherUser?.Email,
        Description = subject.Description,
        Chapters = subject.Chapters
            .OrderBy(c => c.OrderNumber)
            .Select(c => new ChapterListItemDto
            {
                Id = c.Id,
                Title = c.Title,
                OrderNumber = c.OrderNumber
            })
            .ToList()
    };

    private async Task<string> GenerateUniqueCodeAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = RemoveDiacritics(name).ToUpperInvariant();
        var slug = NonAlphaNumeric().Replace(normalized, "");
        var baseCode = slug.Length > 12 ? slug[..12] : slug;
        if (string.IsNullOrEmpty(baseCode))
            baseCode = "MON";

        var code = baseCode;
        var suffix = 1;
        while (await _subjects.CodeExistsAsync(code, cancellationToken))
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
