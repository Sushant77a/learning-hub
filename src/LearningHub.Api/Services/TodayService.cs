using LearningHub.Api.Data;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class TodayCardDto
{
    public int TrackId { get; set; }
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public string? LessonId { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public int Minutes { get; set; }
    public int SharePercent { get; set; }
    public int EstimatedMinutes { get; set; }
    public string LessonStatus { get; set; } = "notstarted";
    public int CurrentStep { get; set; }
    public bool IsDsa { get; set; }
    public string? Topic { get; set; }
    public bool IsDetour { get; set; }
    public bool NeedsCurriculum { get; set; }
    public bool HasTask { get; set; }
    public string? LearnedNote { get; set; }
}

public sealed class PausedTrackDto
{
    public int TrackId { get; set; }
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public string? PausedReason { get; set; }
    public string? ResumeOn { get; set; }
    public string? LastPractised { get; set; }
}

public sealed class TodayDto
{
    public string Date { get; set; } = "";
    public string DateLabel { get; set; } = "";
    public bool IsRestDay { get; set; }
    public string? RestNote { get; set; }
    public List<TodayCardDto> Cards { get; set; } = new();
    public List<PausedTrackDto> PausedTracks { get; set; } = new();
    public DetourDto? Detour { get; set; }
    public bool TimeBoxDecisionDue { get; set; }
    public int DueReviewCount { get; set; }
    public bool ReflectionDue { get; set; }
    public string ReflectionWeekStart { get; set; } = "";
    public List<string> Warnings { get; set; } = new();
    public int MissedDaysShifted { get; set; }
    public int DailyMinutes { get; set; }
    public bool HasActiveTracks { get; set; }
}

public sealed class TodayService(HubDbContext db, ScheduleService schedules, ReviewService reviews)
{
    public async Task<TodayDto> GetAsync(CancellationToken ct = default)
    {
        var today = AppClock.Today;
        var snapshot = await schedules.RebuildAsync(today, ct: ct);
        var settings = await schedules.GetSettingsAsync(ct);
        var restWeekdays = ScheduleService.ParseRestWeekdays(settings.RestWeekdaysCsv);
        var restDay = await db.RestDays.AsNoTracking().FirstOrDefaultAsync(r => r.Date == today, ct);
        var isRestDay = restWeekdays.Contains(today.DayOfWeek) || restDay is not null;

        var tracks = await db.Tracks.AsNoTracking().ToListAsync(ct);
        var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var lessons = await db.Lessons.AsNoTracking().ToDictionaryAsync(l => l.Id, ct);
        var progress = await db.LessonProgress.AsNoTracking()
            .Where(p => p.TrackId != 0)
            .ToListAsync(ct);
        var progressByTrack = progress.GroupBy(p => p.TrackId).ToDictionary(g => g.Key, g => g.ToList());

        var cards = new List<TodayCardDto>();
        if (!isRestDay)
        {
            foreach (var track in tracks.Where(t => t.State == TrackState.Active))
            {
                var planTrack = snapshot.Tracks.FirstOrDefault(t => t.TrackId == track.Id);
                var entries = snapshot.Entries
                    .Where(e => e.TrackId == track.Id && e.Date == Days.Iso(today))
                    .OrderBy(e => e.Status == "done" ? 0 : 1)
                    .ToList();
                var entry = entries.FirstOrDefault();
                var remainingLessons = lessons.Values.Where(l => l.SkillId == track.SkillId).Count();
                if (entry is null && remainingLessons == 0)
                {
                    cards.Add(new TodayCardDto
                    {
                        TrackId = track.Id,
                        SkillId = track.SkillId,
                        SkillName = skills.GetValueOrDefault(track.SkillId)?.Name ?? track.SkillId,
                        NeedsCurriculum = true,
                        Minutes = planTrack?.DailyMinutes ?? 0,
                        SharePercent = planTrack?.SharePercent ?? 0,
                        IsDsa = skills.GetValueOrDefault(track.SkillId)?.IsDsa ?? false
                    });
                    continue;
                }

                if (entry is null) continue;

                var lesson = lessons.GetValueOrDefault(entry.LessonId);
                var lessonProgress = progressByTrack.GetValueOrDefault(track.Id)?
                    .FirstOrDefault(p => p.LessonId == entry.LessonId);
                var lessonFile = LessonContent.Get(lesson);
                cards.Add(new TodayCardDto
                {
                    TrackId = track.Id,
                    SkillId = track.SkillId,
                    SkillName = skills.GetValueOrDefault(track.SkillId)?.Name ?? track.SkillId,
                    LessonId = entry.LessonId,
                    Title = lesson?.Title ?? entry.LessonTitle,
                    Summary = lesson?.Summary,
                    Minutes = entry.Minutes,
                    SharePercent = planTrack?.SharePercent ?? 0,
                    EstimatedMinutes = lesson?.EstimatedMinutes ?? entry.Minutes,
                    LessonStatus = entry.Status == "done"
                        ? "completed"
                        : (lessonProgress?.Status == LessonStatus.InProgress ? "inprogress" : "notstarted"),
                    CurrentStep = lessonProgress?.CurrentStep ?? 0,
                    IsDsa = skills.GetValueOrDefault(track.SkillId)?.IsDsa ?? false,
                    Topic = lesson?.Topic,
                    IsDetour = snapshot.Detour?.TrackId == track.Id,
                    HasTask = lessonFile?.Task is not null,
                    LearnedNote = lessonProgress?.LearnedNote
                });
            }
        }

        var paused = new List<PausedTrackDto>();
        foreach (var track in tracks.Where(t => t.State == TrackState.Paused))
        {
            var planTrack = snapshot.Tracks.FirstOrDefault(t => t.TrackId == track.Id);
            var lastActive = progressByTrack.GetValueOrDefault(track.Id)?.Max(p => p.LastActivityAt);
            paused.Add(new PausedTrackDto
            {
                TrackId = track.Id,
                SkillId = track.SkillId,
                SkillName = skills.GetValueOrDefault(track.SkillId)?.Name ?? track.SkillId,
                PausedReason = track.PausedReason,
                ResumeOn = planTrack?.ResumeOn,
                LastPractised = lastActive is { } last ? Days.Iso(DateOnly.FromDateTime(last)) : null
            });
        }

        var activeCount = tracks.Count(t => t.State == TrackState.Active);
        var warnings = new List<string>();
        if (activeCount > 3)
            warnings.Add($"{activeCount} tracks are active. Daily time per track is getting thin — consider pausing one.");

        var weekStart = Days.StartOfWeek(today);
        var hasThisWeek = await db.Reflections.AnyAsync(r => r.WeekStart == weekStart, ct);
        var previousWeek = weekStart.AddDays(-7);
        var hasPreviousWeek = await db.Reflections.AnyAsync(r => r.WeekStart == previousWeek, ct);
        var reflectionDue = today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? !hasThisWeek
            : today.DayOfWeek == DayOfWeek.Monday && !hasPreviousWeek;
        var reflectionWeek = today.DayOfWeek == DayOfWeek.Monday && !hasPreviousWeek
            ? previousWeek
            : weekStart;

        return new TodayDto
        {
            Date = Days.Iso(today),
            DateLabel = today.ToString("dddd, d MMMM"),
            IsRestDay = isRestDay,
            RestNote = restDay?.Note,
            Cards = cards,
            PausedTracks = paused,
            Detour = snapshot.Detour,
            TimeBoxDecisionDue = snapshot.Detour?.DecisionDue == true,
            DueReviewCount = await reviews.DueCountAsync(today, ct),
            ReflectionDue = reflectionDue,
            ReflectionWeekStart = Days.Iso(reflectionWeek),
            Warnings = warnings,
            MissedDaysShifted = snapshot.MissedDaysShifted,
            DailyMinutes = settings.DailyMinutes,
            HasActiveTracks = activeCount > 0
        };
    }
}

public static class LessonContent
{
    public static Core.Curriculum.LessonFile? Get(Lesson? lesson)
    {
        if (lesson is null || string.IsNullOrWhiteSpace(lesson.ContentJson)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Core.Curriculum.LessonFile>(
                lesson.ContentJson, Core.Curriculum.CurriculumJson.Options);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
