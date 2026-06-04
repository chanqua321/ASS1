namespace BusinessLogic.DTOs;

/// <summary>Một lượt hội thoại gửi cho Ollama (role OpenAI: user | assistant).</summary>
public class ChatHistoryTurnDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}
