using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Model.Data;

/// <summary>
/// Cho phép chạy từ thư mục Model: <c>dotnet ef database update</c> (không cần --startup-project Web).
/// Đọc connection string từ Web/appsettings.json.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .Options;
        return new AppDbContext(options);
    }

    private static string ResolveConnectionString()
    {
        foreach (var webDir in GetWebProjectDirectories())
        {
            if (!Directory.Exists(webDir))
                continue;

            var config = new ConfigurationBuilder()
                .SetBasePath(webDir)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            var cs = config.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(cs))
                return cs;
        }

        throw new InvalidOperationException(
            "Không tìm thấy ConnectionStrings:DefaultConnection. " +
            "Chạy lệnh trong thư mục Model hoặc đảm bảo Web/appsettings.json tồn tại.");
    }

    private static IEnumerable<string> GetWebProjectDirectories()
    {
        var cwd = Directory.GetCurrentDirectory();
        yield return Path.GetFullPath(Path.Combine(cwd, "Web"));
        yield return Path.GetFullPath(Path.Combine(cwd, "..", "Web"));
        yield return Path.GetFullPath(Path.Combine(cwd, "..", "..", "Web"));
    }
}
