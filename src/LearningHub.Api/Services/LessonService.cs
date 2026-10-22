using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Core.Curriculum;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class LessonQuestionDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = "check";
    public string Prompt { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public string? Code { get; set; }
}

public sealed class LessonTrackInfoDto
{
    public int TrackId { get; set; }
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public string State { get; set; } = "active";
}

public sealed class LessonProgressDto
{
    public string Status { get; set; } = "notstarted";
    public int CurrentStep { get; set; }
    public string? LearnedNote { get; set; }
    public int? ConfidenceRating { get; set; }
    public string? ExplainBack { get; set; }
    public string? ExplainGrade { get; set; }
    public bool TaskDone { get; set; }
    public bool BugFixDone { get; set; }
}

public sealed class LessonDetailDto
{
    public string Id { get; set; } = "";
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public string? Topic { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public int EstimatedMinutes { get; set; }
    public bool IsDsa { get; set; }
    public string Theory { get; set; } = "";
    public WorkedExample? WorkedExample { get; set; }
    public string ExplainPrompt { get; set; } = "";
    public LessonTask? Task { get; set; }
    public BugFix? BugFix { get; set; }
    public List<PracticeProblem> Practice { get; set; } = new();
    public List<LessonQuestionDto> Questions { get; set; } = new();
    public List<QuestionDto> Warmup { get; set; } = new();
    public LessonTrackInfoDto Track { get; set; } = new();
    public LessonProgressDto Progress { get; set; } = new();
    public bool IncludeBugFix { get; set; }
    public List<Note> Notes { get; set; } = new();
    public List<Resource> Resources { get; set; } = new();
    public string? PreviousLessonId { get; set; }
    public string? NextLessonId { get; set; }
}

public sealed class CompleteLessonResult
{
    public bool Completed { get; set; }
    public bool SkillCompleted { get; set; }
    public bool TrackCompleted { get; set; }
    public string? NextLessonDate { get; set; }
    public string Message { get; set; } = "";
}

public sealed class LessonService(
    HubDbContext db,
    ReviewService reviews,
    ScheduleService schedules,
    ILogger<LessonService> logger)
{
    public async Task<LessonDetailDto> GetAsync(string lessonId, int? trackId, CancellationToken ct = default)
    {
        var lesson = await db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonId, ct)
                     ?? throw new KeyNotFoundException("Lesson not found.");
        var skill = await db.Skills.AsNoTracking().FirstOrDefaultAsync(s => s.Id == lesson.SkillId, ct)
                    ?? throw new KeyNotFoundException("Skill not found.");
        var content = LessonContent.Get(lesson)
                      ?? throw new InvalidOperationException("This lesson file could not be parsed.");

        var track = await ResolveTrackAsync(lesson.SkillId, trackId, ct);

        var progress = track is null
            ? null
            : await db.LessonProgress.AsNoTracking()
                .FirstOrDefaultAsync(p => p.LessonId == lessonId && p.TrackId == track.Id, ct);

        var questions = await db.Questions.AsNoTracking()
            .Where(q => q.LessonId == lessonId)
            .OrderBy(q => q.Id)
            .ToListAsync(ct);

        var allLessons = await db.Lessons.AsNoTracking()
            .Where(l => l.SkillId == lesson.SkillId)
            .OrderBy(l => l.Order)
            .ToListAsync(ct);

        var warmup = track is null
            ? new List<QuestionDto>()
            : await reviews.BuildWarmupAsync(lessonId, track.Id, AppClock.Today, ct);

        var settings = await schedules.GetSettingsAsync(ct);
        var includeBugFix = content.BugFix is not null
                            && !string.IsNullOrWhiteSpace(content.BugFix.BrokenCode)
                            && progress?.BugFixDone != true
                            && StablePercent(lesson.Id) < Math.Clamp(settings.BugFixFrequency, 0, 100);

        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.LessonId == lessonId)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(ct);
        var resources = await db.Resources.AsNoTracking()
            .Where(r => r.LessonId == lessonId)
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync(ct);

        var index = allLessons.FindIndex(l => l.Id == lessonId);

        return new LessonDetailDto
        {
            Id = lesson.Id,
            SkillId = lesson.SkillId,
            SkillName = skill.Name,
            Topic = lesson.Topic,
            Order = lesson.Order,
            Title = lesson.Title,
            Summary = lesson.Summary,
            EstimatedMinutes = lesson.EstimatedMinutes,
            IsDsa = skill.IsDsa,
            Theory = content.Theory,
            WorkedExample = content.WorkedExample,
            ExplainPrompt = string.IsNullOrWhiteSpace(content.ExplainPrompt)
                ? "Explain the main idea of this lesson in your own words, as if teaching a friend."
                : content.ExplainPrompt,
            Task = content.Task is null
                ? null
                : new LessonTask
                {
                    Title = content.Task.Title,
                    Prompt = content.Task.Prompt,
                    Language = content.Task.Language,
                    StarterCode = content.Task.StarterCode,
                    Requirements = content.Task.Requirements,
                    Solution = ""
                },
            BugFix = includeBugFix
                ? new BugFix
                {
                    Title = content.BugFix!.Title,
                    Prompt = content.BugFix.Prompt,
                    Language = content.BugFix.Language,
                    BrokenCode = content.BugFix.BrokenCode,
                    Explanation = ""
                }
                : null,
            Practice = skill.IsDsa ? content.Dsa?.Practice ?? new List<PracticeProblem>() : new List<PracticeProblem>(),
            Questions = questions.Select(q => new LessonQuestionDto
            {
                Id = q.Id,
                Kind = q.Kind switch
                {
                    QuestionKind.Complexity => "complexity",
                    QuestionKind.Custom => "custom",
                    _ => "check"
                },
                Prompt = q.Prompt,
                Options = ParseList(q.OptionsJson),
                Code = q.Code
            }).ToList(),
            Warmup = warmup,
            Track = track is null
                ? new LessonTrackInfoDto { SkillId = skill.Id, SkillName = skill.Name, State = "notstarted" }
                : new LessonTrackInfoDto
                {
                    TrackId = track.Id,
                    SkillId = skill.Id,
                    SkillName = skill.Name,
                    State = track.State.ToString().ToLowerInvariant()
                },
            Progress = progress is null
                ? new LessonProgressDto()
                : new LessonProgressDto
                {
                    Status = progress.Status.ToString().ToLowerInvariant(),
                    CurrentStep = progress.CurrentStep,
                    LearnedNote = progress.LearnedNote,
                    ConfidenceRating = progress.ConfidenceRating,
                    ExplainBack = progress.ExplainBack,
                    ExplainGrade = progress.ExplainGrade,
                    TaskDone = progress.TaskDone,
                    BugFixDone = progress.BugFixDone
                },
            IncludeBugFix = includeBugFix,
            Notes = notes,
            Resources = resources,
            PreviousLessonId = index > 0 ? allLessons[index - 1].Id : null,
            NextLessonId = index >= 0 && index < allLessons.Count - 1 ? allLessons[index + 1].Id : null
        };
    }

    private async Task<Track?> ResolveTrackAsync(string skillId, int? trackId, CancellationToken ct)
    {
        if (trackId is not null)
            return await db.Tracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);

        return await db.Tracks
            .Where(t => t.SkillId == skillId && t.State == TrackState.Active)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync(ct)
            ?? await db.Tracks.Where(t => t.SkillId == skillId).OrderByDescending(t => t.Id).FirstOrDefaultAsync(ct);
    }

    public async Task<LessonProgress> StartAsync(string lessonId, int trackId, CancellationToken ct = default)
    {
        var progress = await GetOrCreateProgressAsync(lessonId, trackId, ct);
        if (progress.Status == LessonStatus.NotStarted)
        {
            progress.Status = LessonStatus.InProgress;
            progress.CurrentStep = Math.Max(progress.CurrentStep, 1);
            progress.LastActivityAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return progress;
    }

    private async Task<LessonProgress> GetOrCreateProgressAsync(string lessonId, int trackId, CancellationToken ct)
    {
        var progress = await db.LessonProgress
            .FirstOrDefaultAsync(p => p.LessonId == lessonId && p.TrackId == trackId, ct);
        if (progress is not null) return progress;

        if (!await db.Lessons.AnyAsync(l => l.Id == lessonId, ct))
            throw new KeyNotFoundException("Lesson not found.");
        if (!await db.Tracks.AnyAsync(t => t.Id == trackId, ct))
            throw new KeyNotFoundException("Track not found.");

        progress = new LessonProgress
        {
            LessonId = lessonId,
            TrackId = trackId,
            Status = LessonStatus.InProgress,
            CurrentStep = 1
        };
        db.LessonProgress.Add(progress);
        await db.SaveChangesAsync(ct);
        return progress;
    }

    public async Task SaveStepAsync(string lessonId, int trackId, int step, CancellationToken ct = default)
    {
        var progress = await GetOrCreateProgressAsync(lessonId, trackId, ct);
        progress.CurrentStep = Math.Clamp(Math.Max(progress.CurrentStep, step), 1, 8);
        progress.LastActivityAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveExplainAsync(string lessonId, int trackId, string text, CancellationToken ct = default)
    {
        var progress = await GetOrCreateProgressAsync(lessonId, trackId, ct);
        progress.ExplainBack = text;
        progress.CurrentStep = Math.Max(progress.CurrentStep, 5);
        progress.LastActivityAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task LogTaskAsync(
        string lessonId,
        int trackId,
        bool passed,
        string? reasonTag,
        string? note,
        CancellationToken ct = default)
    {
        var progress = await GetOrCreateProgressAsync(lessonId, trackId, ct);
        progress.TaskDone = true;
        progress.LastActivityAt = DateTime.UtcNow;

        if (!passed)
        {
            var lesson = await db.Lessons.AsNoTracking().FirstAsync(l => l.Id == lessonId, ct);
            db.Mistakes.Add(new Mistake
            {
                SkillId = lesson.SkillId,
                LessonId = lessonId,
                TrackId = trackId,
                Kind = MistakeKind.FailedTask,
                ReasonTag = string.IsNullOrWhiteSpace(reasonTag) ? "Task not finished" : reasonTag.Trim(),
                Note = note
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task LogBugFixAsync(
        string lessonId,
        int trackId,
        bool solved,
        string? explanation,
        CancellationToken ct = default)
    {
        var progress = await GetOrCreateProgressAsync(lessonId, trackId, ct);
        progress.BugFixDone = true;
        progress.LastActivityAt = DateTime.UtcNow;

        if (!solved)
        {
            var lesson = await db.Lessons.AsNoTracking().FirstAsync(l => l.Id == lessonId, ct);
            db.Mistakes.Add(new Mistake
            {
                SkillId = lesson.SkillId,
                LessonId = lessonId,
                TrackId = trackId,
                Kind = MistakeKind.BugFix,
                ReasonTag = "Bug not found",
                Note = explanation
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<CompleteLessonResult> CompleteAsync(
        string lessonId,
        int trackId,
        int confidence,
        string? learnedNote,
        CancellationToken ct = default)
    {
        var progress = await GetOrCreateProgressAsync(lessonId, trackId, ct);
        progress.Status = LessonStatus.Completed;
        progress.CurrentStep = 8;
        progress.ConfidenceRating = Math.Clamp(confidence, 1, 5);
        progress.LearnedNote = learnedNote;
        progress.CompletedAt ??= DateTime.UtcNow;
        progress.LastActivityAt = DateTime.UtcNow;

        var today = AppClock.Today;
        var entry = await db.PlanEntries
            .Where(e => e.TrackId == trackId && e.LessonId == lessonId
                        && e.Status == PlanEntryStatus.Planned && e.Date <= today.AddDays(1))
            .OrderBy(e => e.Date)
            .FirstOrDefaultAsync(ct);
        if (entry is not null)
        {
            entry.Status = PlanEntryStatus.Done;
            entry.CompletedAt = DateTime.UtcNow;
        }

        var track = await db.Tracks.FirstAsync(t => t.Id == trackId, ct);
        var lesson = await db.Lessons.AsNoTracking().FirstAsync(l => l.Id == lessonId, ct);
        var totalLessons = await db.Lessons.CountAsync(l => l.SkillId == lesson.SkillId, ct);
        var completed = await db.LessonProgress
            .Where(p => p.TrackId == trackId && p.Status == LessonStatus.Completed)
            .Join(db.Lessons, p => p.LessonId, l => l.Id, (p, l) => l.SkillId)
            .CountAsync(skillId => skillId == lesson.SkillId, ct);

        // The update above is not visible to the query yet; account for it explicitly.
        if (!await db.LessonProgress.AnyAsync(p => p.TrackId == trackId
                                                   && p.LessonId == lessonId
                                                   && p.Status == LessonStatus.Completed, ct))
        {
            completed++;
        }

        var skillCompleted = totalLessons > 0 && completed >= totalLessons;
        if (skillCompleted)
        {
            track.State = TrackState.Completed;
            track.CompletedAt = DateTime.UtcNow;
            await db.PlanEntries
                .Where(e => e.TrackId == trackId && e.Status == PlanEntryStatus.Planned)
                .ExecuteDeleteAsync(ct);
        }

        await db.SaveChangesAsync(ct);

        if (skillCompleted)
        {
            return new CompleteLessonResult
            {
                Completed = true,
                SkillCompleted = true,
                TrackCompleted = true,
                Message = "Skill complete. Take a moment to let that land."
            };
        }

        var snapshot = await schedules.RebuildAsync(today, ct: ct);
        var nextDate = snapshot.Entries
            .Where(e => e.TrackId == trackId && e.Status != "done")
            .OrderBy(e => e.Date)
            .Select(e => e.Date)
            .FirstOrDefault();

        return new CompleteLessonResult
        {
            Completed = true,
            SkillCompleted = skillCompleted,
            TrackCompleted = false,
            NextLessonDate = nextDate,
            Message = nextDate is null
                ? "Lesson complete."
                : $"Lesson complete. Next up on {DateOnly.Parse(nextDate):d MMM}."
        };
    }

    private static int StablePercent(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return (int)(hash % 100);
        }
    }

    private static List<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }
}
