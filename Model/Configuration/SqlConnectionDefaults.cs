using Microsoft.Data.SqlClient;

namespace Model.Configuration;

/// <summary>
/// Chuỗi kết nối SQL Server duy nhất của project: LocalDB + đăng nhập sa.
/// Mọi query/lưu qua EF đều phải dùng connection đã validate ở đây.
/// </summary>
public static class SqlConnectionDefaults
{
    public const string Server = "(localdb)\\MSSQLLocalDB";
    public const string Database = "Assigment1DocDb";
    public const string UserId = "sa";
    public const string Password = "12345";

    public static string ConnectionString { get; } = Build();

    public static string Build() =>
        $"Server={Server};Database={Database};User Id={UserId};Password={Password};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
    /// Chỉ chấp nhận SQL authentication với sa / 12345. Cấm Trusted Connection / Windows auth.

    public static string RequireSaSqlAuthentication(string? configured)
    {
        var cs = string.IsNullOrWhiteSpace(configured) ? ConnectionString : configured.Trim();
        var builder = new SqlConnectionStringBuilder(cs);

        if (builder.IntegratedSecurity)
        {
            throw new InvalidOperationException(
                "Connection string không được dùng Trusted_Connection / Integrated Security. " +
                $"Chỉ dùng User Id={UserId}; Password={Password}.");
        }

        if (string.IsNullOrWhiteSpace(builder.UserID))
        {
            throw new InvalidOperationException(
                $"Connection string phải có User Id={UserId} (không dùng tài khoản Windows).");
        }

        if (!string.Equals(builder.UserID, UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Chỉ cho phép SQL login '{UserId}'. Hiện tại: '{builder.UserID}'.");
        }

        if (builder.Password != Password)
        {
            throw new InvalidOperationException(
                $"Mật khẩu SQL phải là '{Password}' (User Id={UserId}).");
        }

        return cs;
    }
}
