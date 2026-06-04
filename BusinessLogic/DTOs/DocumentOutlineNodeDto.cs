namespace BusinessLogic.DTOs;

public class DocumentOutlineNodeDto
{
    public string Title { get; set; } = string.Empty;
    public int Level { get; set; } = 1; // 1 = mục lớn, 2 = mục nhỏ, ...
    public string ContentPreview { get; set; } = string.Empty;
    public IReadOnlyList<DocumentOutlineNodeDto> Children { get; set; } = Array.Empty<DocumentOutlineNodeDto>();
}

