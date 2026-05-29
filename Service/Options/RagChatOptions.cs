namespace Service.Options;

public class RagChatOptions
{
    public int TopK { get; set; } = 5;
    /// <summary>Ngưỡng chính — với mock embedding nên để thấp (0.1–0.15).</summary>
    public double MinSimilarityScore { get; set; } = 0.12;
    /// <summary>Dùng top chunk khi không đạt ngưỡng chính nhưng vẫn có kết quả retrieve.</summary>
    public double FallbackMinScore { get; set; } = 0.05;
    /// <summary>Điểm khi câu hỏi nhắc tên/mã môn khớp metadata tài liệu.</summary>
    public double MetadataBoostScore { get; set; } = 0.92;
    public int MaxExcerptLength { get; set; } = 280;
    public int MaxHistoryMessages { get; set; } = 6;

    /// <summary>Mặc định không bắt buộc hiển thị trích dẫn; user có thể bật trên UI.</summary>
    public bool IncludeCitationsByDefault { get; set; } = false;

    /// <summary>Số nguồn tối đa khi bật trích dẫn.</summary>
    public int MaxCitations { get; set; } = 3;

    /// <summary>Chỉ trích dẫn chunk có điểm khớp >= ngưỡng này.</summary>
    public double MinCitationScore { get; set; } = 0.35;

    /// <summary>Số chunk lấy khi user hỏi tóm tắt file.</summary>
    public int SummaryTopK { get; set; } = 12;

    /// <summary>Độ dài mỗi đoạn đưa vào prompt tóm tắt.</summary>
    public int SummaryMaxExcerptLength { get; set; } = 700;
}
