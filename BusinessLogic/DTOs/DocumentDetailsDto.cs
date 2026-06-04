namespace BusinessLogic.DTOs;

public class DocumentDetailsDto : DocumentListItemDto
{
    /// <summary>
    /// Nội dung trích xuất (preview) để demo sau khi upload/index.
    /// </summary>
    public string ExtractedTextPreview { get; set; } = string.Empty;

    /// <summary>
    /// Danh sách chunk preview theo thứ tự (để xem “nội dung file” đã được chunk).
    /// </summary>
    public IReadOnlyList<DocumentChunkPreviewDto> ChunkPreviews { get; set; } =
        Array.Empty<DocumentChunkPreviewDto>();

    /// <summary>
    /// Cây mục lục (mục lớn/mục nhỏ) suy ra từ text trích xuất.
    /// </summary>
    public IReadOnlyList<DocumentOutlineNodeDto> Outline { get; set; } =
        Array.Empty<DocumentOutlineNodeDto>();
}

