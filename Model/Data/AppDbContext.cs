using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Model.Configuration;
using Model.Entities;
using Model.Data.Seed;
using Model.Repository;

namespace Model.Data;

/// <summary>DbContext duy nhất của solution — đăng ký DI, migrate và design-time EF đều qua type này.</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<DocumentEmbedding> DocumentEmbeddings => Set<DocumentEmbedding>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<MessageCitation> MessageCitations => Set<MessageCitation>();
    public DbSet<SubjectEnrollment> SubjectEnrollments => Set<SubjectEnrollment>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UserLoginHistory> UserLoginHistories => Set<UserLoginHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DocumentQuiz> DocumentQuizzes => Set<DocumentQuiz>();

    /// <summary>Chạy migration qua instance <see cref="AppDbContext"/> đã đăng ký DI.</summary>
    public static async Task MigrateAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(AppDbContext));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        logger.LogInformation("Database provider: SqlServer ({Context})", nameof(AppDbContext));
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrated successfully (User Id=sa).");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subject>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.TeacherUser)
                .WithMany()
                .HasForeignKey(x => x.TeacherUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Chapter>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.HasOne(x => x.Subject)
                .WithMany(s => s.Chapters)
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(500).IsRequired();
            e.Property(x => x.StoredFileName).HasMaxLength(500).IsRequired();
            e.Property(x => x.FilePath).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(4000);
            e.HasOne(x => x.Subject)
                .WithMany(s => s.Documents)
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Chapter)
                .WithMany(c => c.Documents)
                .HasForeignKey(x => x.ChapterId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.UploadedByUser)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DocumentChunk>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).IsRequired();
            e.HasIndex(x => new { x.DocumentId, x.ChunkIndex }).IsUnique();
            e.HasOne(x => x.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentEmbedding>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ModelName).HasMaxLength(100).IsRequired();
            e.HasOne(x => x.Chunk)
                .WithOne(c => c.Embedding)
                .HasForeignKey<DocumentEmbedding>(x => x.ChunkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300);
            e.HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).IsRequired();
            e.HasIndex(x => new { x.SessionId, x.CreatedAt });
            e.HasOne(x => x.Session)
                .WithMany(s => s.Messages)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MessageCitation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(500).IsRequired();
            e.Property(x => x.ChapterTitle).HasMaxLength(300);
            e.Property(x => x.Excerpt).IsRequired();
            e.HasOne(x => x.Message)
                .WithMany(m => m.Citations)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubjectEnrollment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.SubjectId, x.Email }).IsUnique();
            e.HasOne(x => x.Subject)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<UserLoginHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.LoggedInAt).IsRequired();
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.LoggedInAt });
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(200).IsRequired();
            e.Property(x => x.Action).HasMaxLength(80).IsRequired();
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.Detail).HasMaxLength(1000);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        modelBuilder.Entity<DocumentQuiz>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.QuestionsJson).IsRequired();
            e.HasIndex(x => new { x.DocumentId, x.CreatedAt });
            e.HasOne(x => x.Document)
                .WithMany()
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppUser>().HasData(AppUserSeed.GetUsers());
    }
}

/// <summary>Đăng ký <see cref="AppDbContext"/> + Repository (extension cho <c>builder.Services.AddDataLayer</c>).</summary>
public static class AppDbContextServiceExtensions
{
    public static IServiceCollection AddDataLayer(this IServiceCollection services, string connectionString)
    {
        var validated = SqlConnectionDefaults.RequireSaSqlAuthentication(connectionString);

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(validated, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)));

        services.AddRepositories();
        return services;
    }
}

/// <summary>Design-time factory cho <c>dotnet ef</c> — cùng file với <see cref="AppDbContext"/>.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = SqlConnectionDefaults.RequireSaSqlAuthentication(ResolveConnectionString());
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
            "Đặt trong Model/appsettings.json hoặc Web/appsettings.json.");
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
