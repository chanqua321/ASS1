namespace Model.Entities;

public class UserLoginHistory
{
    public long Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public DateTime LoggedInAt { get; set; } = DateTime.UtcNow;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

