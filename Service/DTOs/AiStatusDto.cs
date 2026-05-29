namespace Service.DTOs;

public class AiStatusDto
{
    public bool ConfiguredForAi { get; set; }
    public bool IsOnline { get; set; }
    public string Provider { get; set; } = "Local";
    public string Model { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
