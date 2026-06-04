using BusinessLogic.DTOs;

namespace Web.ViewModels;

public class DocumentDetailsPageViewModel
{
    public DocumentDetailsDto Document { get; set; } = null!;
    public DocumentQuizDto? LatestQuiz { get; set; }
    public IReadOnlyList<DocumentQuizDto> QuizHistory { get; set; } = Array.Empty<DocumentQuizDto>();
    public bool CanManageQuiz { get; set; }
    public int QuizDefaultQuestionCount { get; set; } = 10;
    public int QuizMinQuestionCount { get; set; } = 3;
    public int QuizMaxQuestionCount { get; set; } = 30;
}
