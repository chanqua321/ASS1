using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.Cookies;
using Model.Data;
using BusinessLogic.IBusinessLogic;
using BusinessLogic.Options;
using BusinessLogic.Logic;

// ═══════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);
var contentRoot = builder.Environment.ContentRootPath;

builder.Services.AddControllersWithViews();

// --- Auth: Teacher/Student (cookie) ---
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TeacherOnly", policy => policy.RequireRole("Teacher"));
});

// --- Model: một AppDbContext duy nhất + Repository ---
builder.Services.AddDataLayer(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings."));

// --- BusinessLogic: Controller → IBusinessLogic → IRepository ---
builder.Services.Configure<DocumentStorageOptions>(options =>
{
    options.UploadPath = Path.Combine(
        contentRoot,
        builder.Configuration["DocumentStorage:UploadPath"] ?? "App_Data/uploads");
});
builder.Services.Configure<RagChatOptions>(builder.Configuration.GetSection("Chat:Rag"));
builder.Services.Configure<AiModelOptions>(builder.Configuration.GetSection("Chat:Ai"));

builder.Services.AddHttpClient<IRagAnswerGenerator, RagAnswerGenerator>(client =>
    client.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddHttpClient<IAiHealthService, AiHealthService>(client =>
    client.Timeout = TimeSpan.FromSeconds(5));

builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped<ITeacherAssignmentService, TeacherAssignmentService>();
builder.Services.AddScoped<IChunkingService, ChunkingService>();
builder.Services.AddScoped<IEmbeddingService, MockEmbeddingService>();
builder.Services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
builder.Services.AddScoped<IRetrievalService, RetrievalService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentSummaryService, DocumentSummaryService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddHttpClient<IQuizService, QuizService>(client =>
    client.Timeout = TimeSpan.FromSeconds(120));

var app = builder.Build();

// --- Khởi tạo DB (Code First migrate + HasData seed Admin) ---
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");

    try
    {
        await TryStartLocalDbAsync(logger);
        await AppDbContext.MigrateAsync(app.Services);
    }
    catch (SqlException ex)
    {
        logger.LogError(ex,
            "Không kết nối SQL Server/LocalDB. Hãy kiểm tra ConnectionStrings:DefaultConnection trong appsettings.");
        throw;
    }

    var aiLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Chat");
    var aiHealth = scope.ServiceProvider.GetRequiredService<IAiHealthService>();
    var aiStatus = await aiHealth.GetStatusAsync();
    if (aiStatus.IsOnline)
        aiLogger.LogInformation("AI ready: {Provider} / {Model} — {Message}", aiStatus.Provider, aiStatus.Model, aiStatus.Message);
    else if (aiStatus.ConfiguredForAi)
        aiLogger.LogWarning("AI chưa sẵn sàng: {Message}", aiStatus.Message);
    else
        aiLogger.LogInformation("Chat mode: {Message}", aiStatus.Message);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Documents}/{action=Index}/{id?}");

app.Run();

// --- Helper LocalDB (chỉ dùng trong Program.cs) ---
static async Task TryStartLocalDbAsync(ILogger logger)
{
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sqllocaldb",
            Arguments = "start MSSQLLocalDB",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = System.Diagnostics.Process.Start(psi);
        if (process is null) return;

        await process.WaitForExitAsync();
        if (process.ExitCode == 0)
            logger.LogInformation("Started LocalDB instance MSSQLLocalDB.");
    }
    catch (Exception ex)
    {
        logger.LogDebug(ex, "Could not auto-start LocalDB.");
    }
}
