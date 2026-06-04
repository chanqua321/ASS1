namespace BusinessLogic.DTOs;

public class DocumentChunkPreviewDto
{
    public int ChunkIndex { get; set; }
    public int TokenCount { get; set; }
    public string Content { get; set; } = string.Empty;
}

