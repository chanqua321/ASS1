using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Service.Implementations;
using Service.Interfaces;

namespace Service;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string uploadRootPath)
    {
        services.Configure<Options.RagChatOptions>(configuration.GetSection("Chat:Rag"));
        services.Configure<Options.AiModelOptions>(configuration.GetSection("Chat:Ai"));

        services.AddHttpClient<RagAnswerGenerator>(client => client.Timeout = TimeSpan.FromSeconds(90));
        services.AddHttpClient<IAiHealthService, AiHealthService>(client => client.Timeout = TimeSpan.FromSeconds(5));

        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IChunkingService, ChunkingService>();
        services.AddScoped<IEmbeddingService, MockEmbeddingService>();
        services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddScoped<IRetrievalService, RetrievalService>();
        services.AddScoped<IRagAnswerGenerator, RagAnswerGenerator>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IEnrollmentService, EnrollmentService>();
        services.AddScoped<IDocumentService>(sp => new DocumentService(
            sp.GetRequiredService<Model.Data.AppDbContext>(),
            sp.GetRequiredService<ISubjectService>(),
            sp.GetRequiredService<IChunkingService>(),
            sp.GetRequiredService<IEmbeddingService>(),
            sp.GetRequiredService<IDocumentTextExtractor>(),
            uploadRootPath));

        return services;
    }
}
