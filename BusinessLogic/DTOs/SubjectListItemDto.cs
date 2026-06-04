namespace BusinessLogic.DTOs;

public class SubjectListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? TeacherUserId { get; set; }
    public string? TeacherEmail { get; set; }
    public string? Description { get; set; }
    public IReadOnlyList<ChapterListItemDto> Chapters { get; set; } = Array.Empty<ChapterListItemDto>();
}

public class ChapterListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
}
