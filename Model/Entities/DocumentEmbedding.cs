namespace Model.Entities;

public class DocumentEmbedding
{
    public int Id { get; set; }
    public int ChunkId { get; set; }
    public string ModelName { get; set; } = "text-embedding-default";
    /// <summary>Vector serialized as JSON array, e.g. [0.12, -0.03, ...]</summary>
    public string VectorJson { get; set; } = "[]";
    public int Dimensions { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DocumentChunk Chunk { get; set; } = null!;
}
