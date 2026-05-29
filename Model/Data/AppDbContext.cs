using Microsoft.EntityFrameworkCore;
using Model.Entities;

namespace Model.Data;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subject>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
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
            e.HasOne(x => x.Subject)
                .WithMany(s => s.Documents)
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Chapter)
                .WithMany(c => c.Documents)
                .HasForeignKey(x => x.ChapterId)
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

        SeedSubjects(modelBuilder);
    }

    private static void SeedSubjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subject>().HasData(
            new Subject { Id = 1, Code = "PRN222", Name = "Building Cross-Platform Back-End", Description = "ASP.NET Core" },
            new Subject { Id = 2, Code = "SWP391", Name = "Software Project", Description = "Capstone" }
        );

        modelBuilder.Entity<Chapter>().HasData(
            new Chapter { Id = 1, SubjectId = 1, Title = "Chương 1: Giới thiệu", OrderNumber = 1 },
            new Chapter { Id = 2, SubjectId = 1, Title = "Chương 2: MVC & EF Core", OrderNumber = 2 },
            new Chapter { Id = 3, SubjectId = 2, Title = "Chương 1: Khởi động dự án", OrderNumber = 1 }
        );
    }
}
