using Microsoft.Extensions.DependencyInjection;
using Model.IRepository;
using Model.UnitOfWork;

namespace Model.Repository;

/// <summary>Đăng ký Repository — chỉ Model layer gọi AppDbContext.</summary>
public static class RepositoryRegistration
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork.IUnitOfWork, global::Model.UnitOfWork.UnitOfWork>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IChunkRepository, ChunkRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IDocumentQuizRepository, DocumentQuizRepository>();
        return services;
    }
}
