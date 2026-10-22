using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Api.Services;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Endpoints;

public sealed record LessonStepRequest(int TrackId, int Step);
public sealed record ExplainRequest(int TrackId, string Text);
public sealed record TaskRequest(int TrackId, bool Passed, string? ReasonTag, string? Note);
public sealed record BugFixRequest(int TrackId, bool Solved, string? Explanation);
public sealed record CompleteRequest(int TrackId, int Confidence, string? LearnedNote);
public sealed record AnswerRequest(int? SelectedIndex, bool? Correct, int? TrackId, string? ReasonTag);
public sealed record CustomQuestionRequest(
    string SkillId,
    string? LessonId,
    string Prompt,
    List<string> Options,
    int CorrectIndex,
    string Explanation,
    List<string>? OptionExplanations);
public sealed record NoteRequest(string? SkillId, string? LessonId, string Title, string Body);
public sealed record NotePatchRequest(string? Title, string? Body);
public sealed record ResourceRequest(
    string? SkillId,
    string? LessonId,
    string Title,
    string Url,
    string? Type,
    string? Notes,
    string? Status);
public sealed record ResourcePatchRequest(string? Title, string? Url, string? Type, string? Notes, string? Status);
public sealed record MistakeRequest(string SkillId, string? LessonId, int? TrackId, string ReasonTag, string? Note);
public sealed record MistakePatchRequest(string ReasonTag, string? Note);
public sealed record ReflectionRequest(string WeekStart, string WentWell, string WasHard, string ToChange);
public sealed record SuggestionStateRequest(string State);
public sealed record AiExplainRequest(string LessonId, string? Focus);
public sealed record AiPracticeRequest(string LessonId);
public sealed record AiGradeRequest(string LessonId, int TrackId, string Answer);
public sealed record AiDebugRequest(string LessonId, string Code, string? Error);
public sealed record AiCurriculumRequest(string SkillId, int? Days, int? MinutesPerDay);
public sealed record AiReflectionRequest(int ReflectionId);

public static class LearningEndpoints
{
    public static void MapLearningHubContent(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        MapLessons(api);
        MapQuestions(api);
        MapMistakes(api);
        MapNotes(api);
        MapResources(api);
        MapReflections(api);
        MapProgress(api);
        MapSearch(api);
        MapAi(api);
    }

    private static void MapLessons(RouteGroupBuilder api)
    {
        api.MapGet("/lessons/{id}", async (string id, int? trackId, LessonService lessons, CancellationToken ct) =>
            Results.Ok(await lessons.GetAsync(id, trackId, ct)));

        api.MapPost("/lessons/{id}/start", async (
            string id,
            LessonStepRequest request,
            LessonService lessons,
            CancellationToken ct) =>
            Results.Ok(await lessons.StartAsync(id, request.TrackId, ct)));

        api.MapPost("/lessons/{id}/step", async (
            string id,
            LessonStepRequest request,
            LessonService lessons,
            CancellationToken ct) =>
        {
            await lessons.SaveStepAsync(id, request.TrackId, request.Step, ct);
            return Results.Ok(new { saved = true });
        });

        api.MapPost("/lessons/{id}/explain", async (
            string id,
            ExplainRequest request,
            LessonService lessons,
            CancellationToken ct) =>
        {
            await lessons.SaveExplainAsync(id, request.TrackId, request.Text, ct);
            return Results.Ok(new { saved = true });
        });

        api.MapPost("/lessons/{id}/task", async (
            string id,
            TaskRequest request,
            LessonService lessons,
            CancellationToken ct) =>
        {
            await lessons.LogTaskAsync(id, request.TrackId, request.Passed, request.ReasonTag, request.Note, ct);
            return Results.Ok(new { saved = true });
        });

        api.MapPost("/lessons/{id}/bugfix", async (
            string id,
            BugFixRequest request,
            LessonService lessons,
            CancellationToken ct) =>
        {
            await lessons.LogBugFixAsync(id, request.TrackId, request.Solved, request.Explanation, ct);
            return Results.Ok(new { saved = true });
        });

        api.MapGet("/lessons/{id}/bugfix/explanation", async (
            string id,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var lesson = await db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct)
                         ?? throw new KeyNotFoundException("Lesson not found.");
            var content = LessonContent.Get(lesson);
            return Results.Ok(new
            {
                title = content?.BugFix?.Title,
                explanation = content?.BugFix?.Explanation,
                solution = content?.BugFix?.BrokenCode
            });
        });

        api.MapPost("/lessons/{id}/complete", async (
            string id,
            CompleteRequest request,
            LessonService lessons,
            CancellationToken ct) =>
            Results.Ok(await lessons.CompleteAsync(id, request.TrackId, request.Confidence, request.LearnedNote, ct)));
    }

    private static void MapQuestions(RouteGroupBuilder api)
    {
        api.MapGet("/questions", async (
            string? skillId,
            string? lessonId,
            bool? customOnly,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var query = db.Questions.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(skillId)) query = query.Where(q => q.SkillId == skillId);
            if (!string.IsNullOrWhiteSpace(lessonId)) query = query.Where(q => q.LessonId == lessonId);
            if (customOnly == true) query = query.Where(q => q.IsCustom);
            var questions = await query.OrderBy(q => q.Id).Take(200).ToListAsync(ct);
            return Results.Ok(questions.Select(q => new
            {
                q.Id,
                q.SkillId,
                q.LessonId,
                kind = q.Kind.ToString().ToLowerInvariant(),
                q.Prompt,
                options = ParseList(q.OptionsJson),
                q.CorrectIndex,
                q.Explanation,
                optionExplanations = ParseList(q.OptionExplanationsJson),
                q.IsCustom
            }));
        });

        api.MapPost("/questions", async (
            CustomQuestionRequest request,
            ReviewService reviews,
            CancellationToken ct) =>
        {
            var question = await reviews.CreateCustomAsync(
                request.SkillId,
                request.LessonId,
                request.Prompt,
                request.Options ?? new List<string>(),
                request.CorrectIndex,
                request.Explanation,
                request.OptionExplanations,
                ct);
            return Results.Created($"/api/questions/{question.Id}", question);
        });

        api.MapDelete("/questions/{id:int}", async (int id, ReviewService reviews, CancellationToken ct) =>
        {
            await reviews.DeleteQuestionAsync(id, ct);
            return Results.Ok(new { deleted = true });
        });

        api.MapPost("/questions/{id:int}/answer", async (
            int id,
            AnswerRequest request,
            ReviewService reviews,
            CancellationToken ct) =>
            Results.Ok(await reviews.AnswerAsync(
                id, request.SelectedIndex, request.Correct, request.TrackId, request.ReasonTag, ct)));

        api.MapGet("/review", async (
            string? skillId,
            int? limit,
            ReviewService reviews,
            CancellationToken ct) =>
            Results.Ok(await reviews.GetDueAsync(skillId, limit ?? 60, AppClock.Today, ct)));
    }

    private static void MapMistakes(RouteGroupBuilder api)
    {
        api.MapGet("/mistakes", async (
            string? skillId,
            string? reason,
            ProgressService progress,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var mistakes = await progress.GetMistakesAsync(skillId, reason, ct);
            var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
            var lessons = await db.Lessons.AsNoTracking().ToDictionaryAsync(l => l.Id, ct);
            return Results.Ok(mistakes.Select(m => new
            {
                m.Id,
                m.SkillId,
                skillName = skills.GetValueOrDefault(m.SkillId)?.Name ?? m.SkillId,
                m.LessonId,
                lessonTitle = m.LessonId is null ? null : lessons.GetValueOrDefault(m.LessonId)?.Title,
                kind = m.Kind.ToString().ToLowerInvariant(),
                m.ReasonTag,
                m.Note,
                m.CreatedAt
            }));
        });

        api.MapPost("/mistakes", async (
            MistakeRequest request,
            ProgressService progress,
            CancellationToken ct) =>
            Results.Ok(await progress.AddMistakeAsync(
                request.SkillId, request.LessonId, request.TrackId, request.ReasonTag, request.Note, ct)));

        api.MapPatch("/mistakes/{id:int}", async (
            int id,
            MistakePatchRequest request,
            ProgressService progress,
            CancellationToken ct) =>
            Results.Ok(await progress.UpdateMistakeAsync(id, request.ReasonTag, request.Note, ct)));

        api.MapGet("/mistakes/summary", async (string? skillId, HubDbContext db, CancellationToken ct) =>
        {
            var query = db.Mistakes.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(skillId)) query = query.Where(m => m.SkillId == skillId);
            var mistakes = await query.ToListAsync(ct);
            var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
            return Results.Ok(mistakes
                .GroupBy(m => new { m.SkillId, m.ReasonTag })
                .Select(g => new
                {
                    skillId = g.Key.SkillId,
                    skillName = skills.GetValueOrDefault(g.Key.SkillId)?.Name ?? g.Key.SkillId,
                    reason = g.Key.ReasonTag,
                    count = g.Count()
                })
                .OrderByDescending(x => x.count)
                .ToList());
        });
    }

    private static void MapNotes(RouteGroupBuilder api)
    {
        api.MapGet("/notes", async (
            string? skillId,
            string? lessonId,
            string? q,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var query = db.Notes.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(skillId)) query = query.Where(n => n.SkillId == skillId);
            if (!string.IsNullOrWhiteSpace(lessonId)) query = query.Where(n => n.LessonId == lessonId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = $"%{q.Trim()}%";
                query = query.Where(n => EF.Functions.Like(n.Title, pattern) || EF.Functions.Like(n.Body, pattern));
            }
            return Results.Ok(await query.OrderByDescending(n => n.UpdatedAt).Take(200).ToListAsync(ct));
        });

        api.MapGet("/notes/{id:int}", async (int id, HubDbContext db, CancellationToken ct) =>
        {
            var note = await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
            return note is null ? Results.NotFound(new { message = "Note not found." }) : Results.Ok(note);
        });

        api.MapPost("/notes", async (NoteRequest request, HubDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title)) throw new ArgumentException("A note title is required.");
            var note = new Note
            {
                SkillId = string.IsNullOrWhiteSpace(request.SkillId) ? null : request.SkillId,
                LessonId = string.IsNullOrWhiteSpace(request.LessonId) ? null : request.LessonId,
                Title = request.Title.Trim(),
                Body = request.Body ?? ""
            };
            db.Notes.Add(note);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/notes/{note.Id}", note);
        });

        api.MapPatch("/notes/{id:int}", async (
            int id,
            NotePatchRequest request,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id, ct)
                       ?? throw new KeyNotFoundException("Note not found.");
            if (!string.IsNullOrWhiteSpace(request.Title)) note.Title = request.Title.Trim();
            if (request.Body is not null) note.Body = request.Body;
            note.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(note);
        });

        api.MapDelete("/notes/{id:int}", async (int id, HubDbContext db, CancellationToken ct) =>
        {
            var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id, ct)
                       ?? throw new KeyNotFoundException("Note not found.");
            db.Notes.Remove(note);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { deleted = true });
        });
    }

    private static void MapResources(RouteGroupBuilder api)
    {
        api.MapGet("/resources", async (
            string? skillId,
            string? lessonId,
            string? q,
            string? type,
            string? status,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var query = db.Resources.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(skillId)) query = query.Where(r => r.SkillId == skillId);
            if (!string.IsNullOrWhiteSpace(lessonId)) query = query.Where(r => r.LessonId == lessonId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = $"%{q.Trim()}%";
                query = query.Where(r => EF.Functions.Like(r.Title, pattern)
                                         || EF.Functions.Like(r.Url, pattern)
                                         || (r.Notes != null && EF.Functions.Like(r.Notes, pattern)));
            }
            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<ResourceType>(type, true, out var resourceType))
                query = query.Where(r => r.Type == resourceType);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ResourceStatus>(status, true, out var resourceStatus))
                query = query.Where(r => r.Status == resourceStatus);
            var resources = await query.OrderByDescending(r => r.UpdatedAt).Take(300).ToListAsync(ct);
            var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
            var lessons = await db.Lessons.AsNoTracking().ToDictionaryAsync(l => l.Id, ct);
            return Results.Ok(resources.Select(r => new
            {
                r.Id,
                r.SkillId,
                skillName = r.SkillId is null ? null : skills.GetValueOrDefault(r.SkillId)?.Name,
                r.LessonId,
                lessonTitle = r.LessonId is null ? null : lessons.GetValueOrDefault(r.LessonId)?.Title,
                r.Title,
                r.Url,
                type = r.Type.ToString().ToLowerInvariant(),
                r.Notes,
                status = r.Status.ToString().ToLowerInvariant(),
                r.UpdatedAt
            }));
        });

        api.MapPost("/resources", async (ResourceRequest request, HubDbContext db, CancellationToken ct) =>
        {
            ValidateResource(request.Title, request.Url);
            var resource = new Resource
            {
                SkillId = string.IsNullOrWhiteSpace(request.SkillId) ? null : request.SkillId,
                LessonId = string.IsNullOrWhiteSpace(request.LessonId) ? null : request.LessonId,
                Title = request.Title.Trim(),
                Url = request.Url.Trim(),
                Type = ParseType(request.Type),
                Notes = request.Notes?.Trim(),
                Status = ParseStatus(request.Status)
            };
            db.Resources.Add(resource);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/resources/{resource.Id}", resource);
        });

        api.MapPatch("/resources/{id:int}", async (
            int id,
            ResourcePatchRequest request,
            HubDbContext db,
            CancellationToken ct) =>
        {
            var resource = await db.Resources.FirstOrDefaultAsync(r => r.Id == id, ct)
                           ?? throw new KeyNotFoundException("Resource not found.");
            if (!string.IsNullOrWhiteSpace(request.Title)) resource.Title = request.Title.Trim();
            if (!string.IsNullOrWhiteSpace(request.Url))
            {
                ValidateResource(resource.Title, request.Url);
                resource.Url = request.Url.Trim();
            }
            if (!string.IsNullOrWhiteSpace(request.Type)) resource.Type = ParseType(request.Type);
            if (!string.IsNullOrWhiteSpace(request.Status)) resource.Status = ParseStatus(request.Status);
            if (request.Notes is not null) resource.Notes = request.Notes.Trim();
            resource.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(resource);
        });

        api.MapDelete("/resources/{id:int}", async (int id, HubDbContext db, CancellationToken ct) =>
        {
            var resource = await db.Resources.FirstOrDefaultAsync(r => r.Id == id, ct)
                           ?? throw new KeyNotFoundException("Resource not found.");
            db.Resources.Remove(resource);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { deleted = true });
        });
    }

    private static void MapReflections(RouteGroupBuilder api)
    {
        api.MapGet("/reflections", async (string? q, ReflectionService reflections, CancellationToken ct) =>
            Results.Ok(await reflections.ListAsync(q, ct)));

        api.MapGet("/reflections/current", async (
            string? weekStart,
            ReflectionService reflections,
            CancellationToken ct) =>
        {
            var week = WorkspaceService.ParseDate(weekStart) ?? AppClock.Today;
            return Results.Ok(await reflections.GetCurrentAsync(week, ct));
        });

        api.MapGet("/reflections/{id:int}", async (int id, ReflectionService reflections, CancellationToken ct) =>
        {
            var reflection = await reflections.GetAsync(id, ct);
            return reflection is null ? Results.NotFound(new { message = "Reflection not found." }) : Results.Ok(reflection);
        });

        api.MapPost("/reflections", async (
            ReflectionRequest request,
            ReflectionService reflections,
            CancellationToken ct) =>
        {
            var week = WorkspaceService.ParseDate(request.WeekStart)
                       ?? throw new ArgumentException("A week start date is required.");
            return Results.Ok(await reflections.SaveAsync(
                week, request.WentWell, request.WasHard, request.ToChange, ct));
        });

        api.MapPost("/reflections/{id:int}/ai-suggestions", async (
            int id,
            AiService ai,
            ReflectionService reflections,
            CancellationToken ct) =>
        {
            var suggestions = await ai.SuggestPlanAdjustmentsAsync(id, ct);
            return Results.Ok(new
            {
                suggestions,
                reflection = await reflections.GetAsync(id, ct)
            });
        });

        api.MapPatch("/reflections/suggestions/{id:int}", async (
            int id,
            SuggestionStateRequest request,
            ReflectionService reflections,
            CancellationToken ct) =>
        {
            await reflections.SetSuggestionStateAsync(id, request.State, ct);
            return Results.Ok(new { saved = true });
        });
    }

    private static void MapProgress(RouteGroupBuilder api)
    {
        api.MapGet("/progress", async (ProgressService progress, CancellationToken ct) =>
            Results.Ok(await progress.GetOverviewAsync(ct)));

        api.MapGet("/progress/streak", async (ProgressService progress, CancellationToken ct) =>
            Results.Ok(await progress.GetStreakAsync(ct)));

        api.MapGet("/progress/weekly", async (
            string? weekStart,
            ProgressService progress,
            CancellationToken ct) =>
        {
            var week = WorkspaceService.ParseDate(weekStart) ?? AppClock.Today;
            return Results.Ok(await progress.GetWeekSummaryAsync(week, ct));
        });
    }

    private static void MapSearch(RouteGroupBuilder api)
    {
        api.MapGet("/search", async (string? q, SearchService search, CancellationToken ct) =>
            Results.Ok(await search.SearchAsync(q ?? "", ct)));
    }

    private static void MapAi(RouteGroupBuilder api)
    {
        api.MapGet("/ai/status", async (AiService ai, CancellationToken ct) =>
            Results.Ok(await ai.GetStatusAsync(ct)));

        api.MapPost("/ai/explain", async (AiExplainRequest request, AiService ai, CancellationToken ct) =>
            Results.Ok(new { text = await ai.ExplainDifferentlyAsync(request.LessonId, request.Focus, ct) }));

        api.MapPost("/ai/practice", async (AiPracticeRequest request, AiService ai, CancellationToken ct) =>
        {
            var questions = await ai.MorePracticeAsync(request.LessonId, ct);
            return Results.Ok(new
            {
                count = questions.Count,
                questions = questions.Select(q => new { q.Id, q.Prompt })
            });
        });

        api.MapPost("/ai/grade", async (AiGradeRequest request, AiService ai, CancellationToken ct) =>
            Results.Ok(await ai.GradeAsync(request.LessonId, request.TrackId, request.Answer, ct)));

        api.MapPost("/ai/debug", async (AiDebugRequest request, AiService ai, CancellationToken ct) =>
            Results.Ok(new { text = await ai.WhyCodeWrongAsync(request.LessonId, request.Code, request.Error, ct) }));

        api.MapPost("/ai/curriculum", async (
            AiCurriculumRequest request,
            AiService ai,
            WorkspaceService workspace,
            CancellationToken ct) =>
        {
            var lessons = await ai.GenerateMiniCurriculumAsync(
                request.SkillId,
                request.Days ?? 5,
                request.MinutesPerDay ?? 30,
                ct);
            var plan = await workspace.StartTrackAsync(request.SkillId, ct);
            return Results.Ok(new { count = lessons.Count, lessons = lessons.Select(l => new { l.Id, l.Title }), plan });
        });

        api.MapPost("/ai/reflection-suggestions", async (
            AiReflectionRequest request,
            AiService ai,
            CancellationToken ct) =>
        {
            var suggestions = await ai.SuggestPlanAdjustmentsAsync(request.ReflectionId, ct);
            return Results.Ok(suggestions);
        });
    }

    private static void ValidateResource(string title, string url)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A resource title is required.");
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("A resource URL is required.");
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Enter a full http:// or https:// URL.");
    }

    private static ResourceType ParseType(string? value)
        => Enum.TryParse<ResourceType>(value, true, out var type) ? type : ResourceType.Article;

    private static ResourceStatus ParseStatus(string? value)
        => Enum.TryParse<ResourceStatus>(value, true, out var status) ? status : ResourceStatus.Unread;

    private static List<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { return new List<string>(); }
    }
}
