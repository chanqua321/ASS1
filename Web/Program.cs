using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Data;
using Service;
using Service.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)));

var uploadPath = Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["DocumentStorage:UploadPath"] ?? "App_Data/uploads");

builder.Services.AddApplicationServices(builder.Configuration, uploadPath);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
        logger.LogInformation("Database migrated successfully.");
    }
    catch (SqlException ex)
    {
        logger.LogError(ex,
            "Không kết nối được SQL. Kiểm tra (localdb)\\MSSQLLocalDB, login sa/12345, và chạy: sqllocaldb start MSSQLLocalDB");
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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Documents}/{action=Index}/{id?}");

app.Run();
