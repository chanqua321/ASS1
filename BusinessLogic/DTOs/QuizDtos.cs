namespace BusinessLogic.DTOs;

public class QuizQuestionDto
{
    public string Question { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public int CorrectIndex { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public class DocumentQuizDto
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string DocumentFileName { get; set; } = string.Empty;
    public string CreatedByEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<QuizQuestionDto> Questions { get; set; } = Array.Empty<QuizQuestionDto>();
}
