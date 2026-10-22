using LearningHub.Api.Data;
using LearningHub.Api.Endpoints;
using LearningHub.Api.Services;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("LEARNINGHUB_URL") ?? "http://localhost:5180");

var connectionString =
    builder.Configuration.GetConnectionString("LearningHub")
    ?? Environment.GetEnvironmentVariable("LEARNINGHUB_DB")
    ?? "Server=127.0.0.1;Port=3310;Database=learninghub;User ID=learninghub;Password=learninghub_dev;";

EnsureDatabase(connectionString);

builder.Services.AddDbContext<HubDbContext>(options => options.UseMySQL(connectionString));
builder.Services.AddScoped<CurriculumService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<TodayService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<LessonService>();
builder.Services.AddScoped<SkillService>();
builder.Services.AddScoped<AiService>(sp => new AiService(
    sp.GetRequiredService<HubDbContext>(),
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<CurriculumService>(),
    sp.GetRequiredService<ScheduleService>(),
    sp.GetRequiredService<IHttpClientFactory>(),
    sp.GetRequiredService<ILogger<AiService>>()
));
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<ReflectionService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddScoped<IcsService>();
builder.Services.AddScoped<GoogleCalendarService>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddHttpClient("ai");
builder.Services.AddHttpClient("google");
builder.Services.AddCors(options =>
{
    options.AddPolicy("local", policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:5180", "http://127.0.0.1:5180")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("local");
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (AiUnavailableException ex)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { message = ex.Message });
    }
    catch (KeyNotFoundException ex)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { message = ex.Message });
    }
    catch (ArgumentException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Unhandled request error");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { message = "Something went wrong on the local server." });
    }
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapLearningHub();
app.MapLearningHubContent();

app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = 404;
        return;
    }

    var index = Path.Combine(app.Environment.WebRootPath ?? "", "index.html");
    if (File.Exists(index))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(index);
    }
    else
    {
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync(
            "Learning Hub API is running. The client has not been built yet — run start.cmd, or npm run build in client/.");
    }
});

await InitializeAsync(app);

app.Run();

static void EnsureDatabase(string connectionString)
{
    var builder = new MySqlConnectionStringBuilder(connectionString);
    var database = builder.Database;
    if (string.IsNullOrWhiteSpace(database)) return;

    builder.Database = "";
    var attempt = 0;
    while (true)
    {
        try
        {
            using var connection = new MySqlConnection(builder.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                $"CREATE DATABASE IF NOT EXISTS `{database.Replace("`", "``")}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            command.ExecuteNonQuery();
            return;
        }
        catch (MySqlException ex) when (attempt < 8)
        {
            attempt++;
            Console.WriteLine($"Waiting for MySQL ({ex.Message.Trim()})...");
            Thread.Sleep(2000);
        }
    }
}

static async Task InitializeAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
    var curriculum = scope.ServiceProvider.GetRequiredService<CurriculumService>();
    var schedules = scope.ServiceProvider.GetRequiredService<ScheduleService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await db.Database.EnsureCreatedAsync();
    var sync = await curriculum.SyncAsync(db);
    logger.LogInformation("Curriculum synced: {Skills} skills, {Lessons} lessons, {Errors} issues",
        sync.Skills, sync.Lessons, sync.Errors.Count);

    var settings = await schedules.GetSettingsAsync();

    if (!await db.Tracks.AnyAsync())
    {
        settings.DailyMinutes = 120;
        settings.RestWeekdaysCsv = "Saturday,Sunday";
        settings.StartDate = AppClock.Today;
        var csharp = await db.Skills.FirstOrDefaultAsync(s => s.Id == "csharp");
        var dsa = await db.Skills.FirstOrDefaultAsync(s => s.Id == "dsa");
        if (csharp is not null)
            db.Tracks.Add(new Track
            {
                SkillId = csharp.Id,
                State = TrackState.Active,
                CreatedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow
            });
        if (dsa is not null)
            db.Tracks.Add(new Track
            {
                SkillId = dsa.Id,
                State = TrackState.Active,
                CreatedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow
            });
    }

    if (!await db.Resources.AnyAsync())
    {
        db.Resources.AddRange(
            new Resource
            {
                SkillId = "csharp",
                Title = "C# documentation",
                Url = "https://learn.microsoft.com/dotnet/csharp/",
                Type = ResourceType.Docs,
                Notes = "The language reference. Keep this open while reading lessons.",
                Status = ResourceStatus.Reading
            },
            new Resource
            {
                SkillId = "csharp",
                Title = ".NET documentation",
                Url = "https://learn.microsoft.com/dotnet/",
                Type = ResourceType.Docs,
                Notes = "Runtime, SDK and platform guides.",
                Status = ResourceStatus.Unread
            },
            new Resource
            {
                SkillId = "dsa",
                Title = "Big-O cheat sheet",
                Url = "https://www.bigocheatsheet.com/",
                Type = ResourceType.Article,
                Notes = "Quick complexity reference for common data structures.",
                Status = ResourceStatus.Unread
            });
    }

    await db.SaveChangesAsync();
    await schedules.RebuildAsync(AppClock.Today);
}
