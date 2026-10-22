using System.Text;
using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Api.Services;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Endpoints;

public sealed record SkillCreateRequest(string Name, string? Area, string? Description);
public sealed record SkillPatchRequest(string? Name, string? Area, string? Description);
public sealed record TrackCreateRequest(string SkillId);
public sealed record StateRequest(string State);
public sealed record TopicRequest(string? Topic);
public sealed record EndTimeBoxRequest(string Choice, string? ExtendTo);
public sealed record RestDayRequest(string Date, string? Note);
public sealed record SettingsPatchRequest(
    int? DailyMinutes,
    string? RestWeekdays,
    string? StartDate,
    int? BugFixFrequency,
    string? AiProvider,
    string? Theme);
public sealed record CurriculumGenerateRequest(string SkillId, int? Days, int? MinutesPerDay);
public sealed record PlanRangeRequest(string? From, string? To);

public static class AppEndpoints
{
    public static void MapLearningHub(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            time = DateTime.Now.ToString("O"),
            today = Days.Iso(AppClock.Today)
        }));

        MapSettings(api);
        MapSkills(api);
        MapTracks(api);
        MapPlan(api);
        MapTimeBoxes(api);
        MapToday(api);
        MapCalendar(api);
    }

    private static void MapSettings(RouteGroupBuilder api)
    {
        api.MapGet("/settings", async (
            ScheduleService schedules,
            AiService ai,
            CurriculumService curriculum,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var settings = await schedules.GetSettingsAsync(ct);
            return Results.Ok(await BuildSettingsAsync(settings, ai, curriculum, db, ct));
        });

        api.MapPatch("/settings", async (
            SettingsPatchRequest request,
            ScheduleService schedules,
            AiService ai,
            CurriculumService curriculum,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var settings = await schedules.GetSettingsAsync(ct);
            var scheduleChanged = false;

            if (request.DailyMinutes is { } minutes)
            {
                if (minutes < 15 || minutes > 720) throw new ArgumentException("Daily minutes must be between 15 and 720.");
                settings.DailyMinutes = minutes;
                scheduleChanged = true;
            }
            if (request.RestWeekdays is not null)
            {
                settings.RestWeekdaysCsv = ScheduleService.SerializeRestWeekdays(
                    ScheduleService.ParseRestWeekdays(request.RestWeekdays));
                scheduleChanged = true;
            }
            if (WorkspaceService.ParseDate(request.StartDate) is { } start)
            {
                settings.StartDate = start;
                scheduleChanged = true;
            }
            if (request.BugFixFrequency is { } frequency)
                settings.BugFixFrequency = Math.Clamp(frequency, 0, 100);
            if (request.AiProvider is not null)
            {
                var provider = request.AiProvider.Trim().ToLowerInvariant();
                if (provider is not ("" or "openai" or "anthropic" or "ollama" or "openai-compatible"))
                    throw new ArgumentException("Unknown AI provider.");
                settings.AiProviderOverride = string.IsNullOrWhiteSpace(provider) ? null : provider;
            }
            if (!string.IsNullOrWhiteSpace(request.Theme))
                settings.Theme = request.Theme is "light" or "dark" or "system" ? request.Theme : "system";
            settings.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            if (scheduleChanged)
                await schedules.RebuildAsync(AppClock.Today, ct: ct);

            return Results.Ok(await BuildSettingsAsync(settings, ai, curriculum, db, ct));
        });

        api.MapPost("/settings/reload-curriculum", async (
            CurriculumService curriculum,
            ScheduleService schedules,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var result = await curriculum.SyncAsync(db, ct);
            var plan = await schedules.RebuildAsync(AppClock.Today, ct: ct);
            return Results.Ok(new { sync = result, plan });
        });

        api.MapGet("/export", async (BackupService backups, CancellationToken ct) =>
        {
            var document = await backups.ExportAsync(ct);
            var json = JsonSerializer.Serialize(document, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            return Results.File(bytes, "application/json", $"learning-hub-backup-{AppClock.Today:yyyy-MM-dd}.json");
        });

        api.MapPost("/import", async (HttpRequest request, BackupService backups, HubDbContext db, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync(ct);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            BackupDocument? document;
            try
            {
                document = JsonSerializer.Deserialize<BackupDocument>(json, options);
            }
            catch (JsonException)
            {
                throw new ArgumentException("That file is not valid JSON.");
            }
            if (document is null) throw new ArgumentException("The backup file is empty.");
            var result = await backups.ImportAsync(document, ct);
            return Results.Ok(result);
        });
    }

    private static async Task<object> BuildSettingsAsync(
        PlanSettings settings,
        AiService ai,
        CurriculumService curriculum,
        HubDbContext db,
        CancellationToken ct)
    {
        return new
        {
            dailyMinutes = settings.DailyMinutes,
            restWeekdays = settings.RestWeekdaysCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            startDate = Days.Iso(settings.StartDate),
            bugFixFrequency = settings.BugFixFrequency,
            aiProvider = settings.AiProviderOverride,
            theme = settings.Theme,
            ai = await ai.GetStatusAsync(ct),
            counts = new
            {
                skills = await db.Skills.CountAsync(ct),
                lessons = await db.Lessons.CountAsync(ct),
                notes = await db.Notes.CountAsync(ct),
                resources = await db.Resources.CountAsync(ct),
                mistakes = await db.Mistakes.CountAsync(ct)
            },
            activeTracks = await db.Tracks.CountAsync(t => t.State == TrackState.Active, ct),
            curriculumRoot = curriculum.RootPath,
            curriculumErrors = curriculum.LastErrors
        };
    }

    private static void MapSkills(RouteGroupBuilder api)
    {
        api.MapGet("/skills", async (SkillService skills, CancellationToken ct) =>
            Results.Ok(await skills.GetAllAsync(recordHistory: true, ct)));

        api.MapGet("/skills/{id}", async (string id, SkillService skills, CancellationToken ct) =>
        {
            var detail = await skills.GetDetailAsync(id, ct);
            return detail is null ? Results.NotFound(new { message = "Skill not found." }) : Results.Ok(detail);
        });

        api.MapPost("/skills", async (
            SkillCreateRequest request,
            HubDbContext db,
            CurriculumService curriculum,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("A skill name is required.");
            var baseSlug = NameUtil.Slug(request.Name);
            if (string.IsNullOrWhiteSpace(baseSlug)) throw new ArgumentException("Use letters or numbers in the name.");
            var slug = baseSlug;
            var suffix = 2;
            while (await db.Skills.AnyAsync(s => s.Id == slug, ct))
                slug = $"{baseSlug}-{suffix++}";

            var maxOrder = await db.Skills.MaxAsync(s => (int?)s.SortOrder, ct) ?? 0;
            var skill = new Skill
            {
                Id = slug,
                Name = request.Name.Trim(),
                Area = string.IsNullOrWhiteSpace(request.Area) ? "My skills" : request.Area.Trim(),
                Description = request.Description?.Trim() ?? "",
                IsCustom = true,
                SortOrder = maxOrder + 1
            };
            db.Skills.Add(skill);
            await db.SaveChangesAsync(ct);

            try
            {
                Directory.CreateDirectory(Path.Combine(curriculum.RootPath, slug));
            }
            catch (Exception ex)
            {
                // A read-only curriculum folder should not stop skill creation.
                loggerFactory.CreateLogger("Skills").LogWarning(ex, "Could not create skill folder for {Skill}", slug);
            }

            return Results.Created($"/api/skills/{slug}", skill);
        });

        api.MapPatch("/skills/{id}", async (
            string id,
            SkillPatchRequest request,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == id, ct)
                        ?? throw new KeyNotFoundException("Skill not found.");
            if (!string.IsNullOrWhiteSpace(request.Name)) skill.Name = request.Name.Trim();
            if (!string.IsNullOrWhiteSpace(request.Area)) skill.Area = request.Area.Trim();
            if (request.Description is not null) skill.Description = request.Description.Trim();
            await db.SaveChangesAsync(ct);
            return Results.Ok(skill);
        });

        api.MapPost("/skills/{id}/preview-track", async (
            string id,
            WorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await workspace.PreviewNewTrackAsync(id, ct)));

        api.MapPost("/skills/{id}/study", async (
            string id,
            WorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await workspace.StartTrackAsync(id, ct)));

        api.MapPost("/skills/{id}/generate-curriculum", async (
            string id,
            CurriculumGenerateRequest request,
            AiService ai,
            ScheduleService schedules,
            HubDbContext db,
            WorkspaceService workspace,
            CancellationToken ct) =>
        {
            var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == id, ct)
                        ?? throw new KeyNotFoundException("Skill not found.");
            var timeBox = await db.TimeBoxes.AsNoTracking()
                .Where(t => t.SkillId == id && t.Status == TimeBoxStatus.Active)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(ct);
            var settings = await schedules.GetSettingsAsync(ct);
            var days = request.Days
                       ?? (timeBox is null
                           ? 5
                           : Math.Max(1, timeBox.EndDate.DayNumber - AppClock.Today.DayNumber + 1));
            var minutes = request.MinutesPerDay
                          ?? (timeBox is null
                              ? settings.DailyMinutes
                              : Math.Max(15, settings.DailyMinutes * Math.Clamp(timeBox.DetourPercent, 5, 95) / 100));
            var lessons = await ai.GenerateMiniCurriculumAsync(skill.Id, Math.Clamp(days, 2, 8), minutes, ct);
            var plan = await workspace.StartTrackAsync(skill.Id, ct);
            return Results.Ok(new
            {
                count = lessons.Count,
                lessons = lessons.Select(l => new { l.Id, l.Title, l.Summary, l.Order }),
                plan
            });
        });
    }

    private static void MapTracks(RouteGroupBuilder api)
    {
        api.MapGet("/tracks", async (HubDbContext db, CancellationToken ct) =>
        {
            var tracks = await db.Tracks.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct);
            var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
            return Results.Ok(tracks.Select(t => new
            {
                t.Id,
                t.SkillId,
                skillName = skills.GetValueOrDefault(t.SkillId)?.Name ?? t.SkillId,
                state = t.State.ToString().ToLowerInvariant(),
                t.PausedReason,
                t.TimeSplitPercent,
                t.DsaOverride,
                t.CreatedAt,
                t.StartedAt,
                t.CompletedAt
            }));
        });

        api.MapPost("/tracks/preview", async (TrackCreateRequest request, WorkspaceService workspace, CancellationToken ct) =>
            Results.Ok(await workspace.PreviewNewTrackAsync(request.SkillId, ct)));

        api.MapPost("/tracks", async (TrackCreateRequest request, WorkspaceService workspace, CancellationToken ct) =>
            Results.Ok(await workspace.StartTrackAsync(request.SkillId, ct)));

        api.MapPost("/tracks/{id:int}/state/preview", async (
            int id,
            StateRequest request,
            WorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await workspace.PreviewTrackStateAsync(id, request.State, ct)));

        api.MapPost("/tracks/{id:int}/state", async (
            int id,
            StateRequest request,
            WorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await workspace.SetTrackStateAsync(id, request.State, ct)));

        api.MapPost("/tracks/{id:int}/dsa/complete-topic", async (
            int id,
            TopicRequest request,
            WorkspaceService workspace,
            CancellationToken ct) =>
        {
            var (plan, topic) = await workspace.CompleteDsaTopicAsync(id, request.Topic, ct);
            return Results.Ok(new { plan, topic });
        });
    }

    private static void MapPlan(RouteGroupBuilder api)
    {
        api.MapGet("/plan", async (ScheduleService schedules, CancellationToken ct) =>
            Results.Ok(await schedules.RebuildAsync(AppClock.Today, ct: ct)));

        api.MapPost("/plan/preview", async (
            PlanChangeRequest request,
            WorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await workspace.PreviewPlanAsync(request, ct)));

        api.MapPost("/plan/apply", async (
            PlanChangeRequest request,
            WorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await workspace.ApplyPlanAsync(request, ct)));

        api.MapPost("/plan/rest-days", async (
            RestDayRequest request,
            WorkspaceService workspace,
            CancellationToken ct) =>
        {
            var date = WorkspaceService.ParseDate(request.Date)
                       ?? throw new ArgumentException("A date is required.");
            return Results.Ok(await workspace.AddRestDayAsync(date, request.Note, ct));
        });

        api.MapDelete("/plan/rest-days/{date}", async (
            string date,
            WorkspaceService workspace,
            CancellationToken ct) =>
        {
            var parsed = WorkspaceService.ParseDate(date)
                         ?? throw new ArgumentException("A valid date is required.");
            return Results.Ok(await workspace.RemoveRestDayAsync(parsed, ct));
        });
    }

    private static void MapTimeBoxes(RouteGroupBuilder api)
    {
        api.MapGet("/timeboxes", async (HubDbContext db, ScheduleService schedules, CancellationToken ct) =>
        {
            var timeBoxes = await db.TimeBoxes.AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .ToListAsync(ct);
            var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
            var tracks = await db.Tracks.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
            return Results.Ok(timeBoxes.Select(t => new
            {
                t.Id,
                t.SkillId,
                skillName = skills.GetValueOrDefault(t.SkillId)?.Name ?? t.SkillId,
                t.TrackId,
                trackState = tracks.GetValueOrDefault(t.TrackId)?.State.ToString().ToLowerInvariant(),
                t.StartDate,
                t.EndDate,
                mode = t.Mode == TimeBoxMode.PauseOthers ? "pause" : "parallel",
                t.DetourPercent,
                status = t.Status.ToString().ToLowerInvariant(),
                t.Label,
                pausedTrackIds = ScheduleService.ParseIntList(t.PausedTrackIdsJson)
            }));
        });

        api.MapPost("/timeboxes/preview", async (
            TimeBoxRequest request,
            WorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await workspace.PreviewTimeBoxAsync(request, ct)));

        api.MapPost("/timeboxes", async (
            TimeBoxRequest request,
            WorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await workspace.ApplyTimeBoxAsync(request, ct)));

        api.MapPost("/timeboxes/{id:int}/end", async (
            int id,
            EndTimeBoxRequest request,
            WorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await workspace.EndTimeBoxAsync(id, request.Choice, request.ExtendTo, ct)));
    }

    private static void MapToday(RouteGroupBuilder api)
    {
        api.MapGet("/today", async (TodayService today, CancellationToken ct) =>
            Results.Ok(await today.GetAsync(ct)));

        api.MapGet("/bootstrap", async (
            TodayService today,
            AiService ai,
            CurriculumService curriculum,
            ScheduleService schedules,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var settings = await schedules.GetSettingsAsync(ct);
            return Results.Ok(new
            {
                today = await today.GetAsync(ct),
                settings = await BuildSettingsAsync(settings, ai, curriculum, db, ct),
                serverDate = Days.Iso(AppClock.Today)
            });
        });
    }

    private static void MapCalendar(RouteGroupBuilder api)
    {
        api.MapGet("/calendar/ics", async (
            string? from,
            string? to,
            IcsService ics,
            CancellationToken ct) =>
        {
            var start = WorkspaceService.ParseDate(from) ?? AppClock.Today.AddDays(-7);
            var end = WorkspaceService.ParseDate(to) ?? AppClock.Today.AddDays(120);
            if (end < start) throw new ArgumentException("The end date must be after the start date.");
            var content = await ics.BuildAsync(start, end, ct);
            var bytes = Encoding.UTF8.GetBytes(content);
            return Results.File(bytes, "text/calendar; charset=utf-8", "learning-hub-schedule.ics");
        });

        api.MapGet("/calendar/google/status", async (GoogleCalendarService google, CancellationToken ct) =>
            Results.Ok(await google.GetStatusAsync(ct)));

        api.MapGet("/calendar/google/connect", (GoogleCalendarService google) =>
        {
            try
            {
                return Results.Redirect(google.BuildAuthUrl());
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        api.MapGet("/calendar/google/callback", async (
            string? code,
            string? state,
            GoogleCalendarService google,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
                return Results.Redirect("/settings?calendar=error&message=Missing%20Google%20code");
            try
            {
                var redirect = await google.HandleCallbackAsync(code, state, ct);
                return Results.Redirect(redirect);
            }
            catch (Exception ex)
            {
                return Results.Redirect($"/settings?calendar=error&message={Uri.EscapeDataString(ex.Message)}");
            }
        });

        api.MapPost("/calendar/google/push", async (GoogleCalendarService google, CancellationToken ct) =>
            Results.Ok(await google.PushAsync(ct)));

        api.MapPost("/calendar/google/disconnect", async (GoogleCalendarService google, CancellationToken ct) =>
        {
            await google.DisconnectAsync(ct);
            return Results.Ok(new { message = "Google Calendar disconnected." });
        });
    }
}
