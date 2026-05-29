namespace Model.Entities;

public class DocumentChunk
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }

    public Document Document { get; set; } = null!;
    public DocumentEmbedding? Embedding { get; set; }
}
