using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Model.Data;

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
        foreach (var basePath in GetConfigurationBasePaths())
        {
            if (!Directory.Exists(basePath))
                continue;

            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
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
            "Đặt connection string trong Model/appsettings.json hoặc Web/appsettings.json.");
    }

    private static IEnumerable<string> GetConfigurationBasePaths()
    {
        var cwd = Directory.GetCurrentDirectory();
        yield return cwd;
        yield return Path.GetFullPath(Path.Combine(cwd, "Model"));
        yield return Path.GetFullPath(Path.Combine(cwd, "..", "Model"));
        yield return Path.GetFullPath(Path.Combine(cwd, "Web"));
        yield return Path.GetFullPath(Path.Combine(cwd, "..", "Web"));
    }
}
