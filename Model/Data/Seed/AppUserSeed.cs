using Model.Entities;

namespace Model.Data.Seed;

/// Dữ liệu seed Code First 

public static class AppUserSeed
{
    public const int AdminId = 1;

    public static readonly DateTime SeedCreatedAt = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// Mật khẩu 12345 
    public const string AdminPasswordHash =
        "100000.RWR1QUlfVXNlclNlZWQhIQ==.Bexafofd0/xrgmR+AgoioivdJ1P/cIauSK07DRkGD3U=";

    public static AppUser[] GetUsers() =>
    [
        new AppUser
        {
            Id = AdminId,
            Email = "admin@gmail.com",
            PasswordHash = AdminPasswordHash,
            Role = "Admin",
            CreatedAt = SeedCreatedAt
        }
    ];
}
