using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class MistakePatternDto
{
    public string Reason { get; set; } = "";
    public int Count { get; set; }
    public string? SkillId { get; set; }
    public string? SkillName { get; set; }
}

public sealed class ConfidenceChangeDto
{
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public double? From { get; set; }
    public double? To { get; set; }
    public double Change { get; set; }
}

public sealed class WeeklySummaryDto
{
    public string WeekStart { get; set; } = "";
    public string WeekEnd { get; set; } = "";
    public int LessonsDone { get; set; }
    public int LessonsPlanned { get; set; }
    public int MinutesDone { get; set; }
    public int MinutesPlanned { get; set; }
    public int StudyDays { get; set; }
    public int RestDays { get; set; }
    public int ReviewsAnswered { get; set; }
    public double Accuracy { get; set; }
    public int MistakesLogged { get; set; }
    public List<MistakePatternDto> MistakePatterns { get; set; } = new();
    public List<ConfidenceChangeDto> ConfidenceChanges { get; set; } = new();
}

public sealed class StreakDto
{
    public int Current { get; set; }
    public int Longest { get; set; }
    public List<string> StudyDates { get; set; } = new();
}

public sealed class ProgressOverviewDto
{
    public StreakDto Streak { get; set; } = new();
    public WeeklySummaryDto ThisWeek { get; set; } = new();
    public int TotalLessonsDone { get; set; }
    public int TotalMinutes { get; set; }
    public double OverallAccuracy { get; set; }
    public int TotalMistakes { get; set; }
    public List<MistakePatternDto> MistakePatterns { get; set; } = new();
    public List<SkillProgressDto> Skills { get; set; } = new();
    public List<DailyMinutesDto> Last14Days { get; set; } = new();
}

public sealed class SkillProgressDto
{
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public int LessonsDone { get; set; }
    public int LessonsTotal { get; set; }
    public double Confidence { get; set; }
    public List<ConfidencePointDto> History { get; set; } = new();
}

public sealed class DailyMinutesDto
{
    public string Date { get; set; } = "";
    public int Minutes { get; set; }
}

public sealed class ProgressService(HubDbContext db, ScheduleService schedules)
{
    public async Task<StreakDto> GetStreakAsync(CancellationToken ct = default)
    {
        var today = AppClock.Today;
        var settings = await schedules.GetSettingsAsync(ct);
        var restWeekdays = ScheduleService.ParseRestWeekdays(settings.RestWeekdaysCsv);
        var restDates = (await db.RestDays.AsNoTracking().Select(r => r.Date).ToListAsync(ct)).ToHashSet();
        var completedDates = await db.LessonProgress.AsNoTracking()
            .Where(p => p.CompletedAt != null)
            .Select(p => p.CompletedAt!.Value)
            .ToListAsync(ct);
        var answerDates = await db.AnswerRecords.AsNoTracking()
            .Select(a => a.AnsweredAt)
            .ToListAsync(ct);
        var studyDates = completedDates.Concat(answerDates)
            .Select(d => DateOnly.FromDateTime(d))
            .ToHashSet();

        bool IsRest(DateOnly d) => restWeekdays.Contains(d.DayOfWeek) || restDates.Contains(d);

        var cursor = today;
        if (!studyDates.Contains(cursor) && !IsRest(cursor))
            cursor = cursor.AddDays(-1);
        var current = 0;
        for (var i = 0; i < 5000 && cursor >= today.AddYears(-10); i++)
        {
            if (IsRest(cursor))
            {
                cursor = cursor.AddDays(-1);
                continue;
            }
            if (!studyDates.Contains(cursor)) break;
            current++;
            cursor = cursor.AddDays(-1);
        }

        var ordered = studyDates.OrderBy(d => d).ToList();
        var longest = 0;
        var run = 0;
        DateOnly? previous = null;
        foreach (var date in ordered)
        {
            if (previous is null)
            {
                run = 1;
            }
            else
            {
                var gap = date.DayNumber - previous.Value.DayNumber;
                var onlyRestBetween = true;
                for (var d = previous.Value.AddDays(1); d < date; d = d.AddDays(1))
                {
                    if (!IsRest(d)) { onlyRestBetween = false; break; }
                }
                run = gap <= 1 || onlyRestBetween ? run + 1 : 1;
            }
            longest = Math.Max(longest, run);
            previous = date;
        }

        return new StreakDto
        {
            Current = current,
            Longest = longest,
            StudyDates = ordered.TakeLast(120).Select(Days.Iso).ToList()
        };
    }

    public async Task<WeeklySummaryDto> GetWeekSummaryAsync(DateOnly weekStart, CancellationToken ct = default)
    {
        weekStart = Days.StartOfWeek(weekStart);
        var weekEnd = weekStart.AddDays(6);
        var entries = await db.PlanEntries.AsNoTracking()
            .Where(e => e.Date >= weekStart && e.Date <= weekEnd)
            .ToListAsync(ct);
        var answers = await db.AnswerRecords.AsNoTracking()
            .Where(a => a.AnsweredAt >= weekStart.ToDateTime(TimeOnly.MinValue)
                        && a.AnsweredAt < weekEnd.AddDays(1).ToDateTime(TimeOnly.MinValue))
            .ToListAsync(ct);
        var mistakes = await db.Mistakes.AsNoTracking()
            .Where(m => m.CreatedAt >= weekStart.ToDateTime(TimeOnly.MinValue)
                        && m.CreatedAt < weekEnd.AddDays(1).ToDateTime(TimeOnly.MinValue))
            .ToListAsync(ct);
        var confidence = await db.ConfidenceHistory.AsNoTracking()
            .Where(c => c.Date >= weekStart && c.Date <= weekEnd)
            .ToListAsync(ct);
        var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var settings = await schedules.GetSettingsAsync(ct);
        var restWeekdays = ScheduleService.ParseRestWeekdays(settings.RestWeekdaysCsv);
        var restDates = (await db.RestDays.AsNoTracking()
                .Where(r => r.Date >= weekStart && r.Date <= weekEnd)
                .Select(r => r.Date)
                .ToListAsync(ct)).ToHashSet();

        var done = entries.Where(e => e.Status == PlanEntryStatus.Done).ToList();
        var studyDates = done.Select(e => e.Date)
            .Concat(answers.Select(a => DateOnly.FromDateTime(a.AnsweredAt)))
            .ToHashSet();

        return new WeeklySummaryDto
        {
            WeekStart = Days.Iso(weekStart),
            WeekEnd = Days.Iso(weekEnd),
            LessonsDone = done.Count,
            LessonsPlanned = entries.Count,
            MinutesDone = done.Sum(e => e.Minutes),
            MinutesPlanned = entries.Sum(e => e.Minutes),
            StudyDays = studyDates.Count,
            RestDays = Days.Range(weekStart, weekEnd).Count(d =>
                restWeekdays.Contains(d.DayOfWeek) || restDates.Contains(d)),
            ReviewsAnswered = answers.Count,
            Accuracy = answers.Count == 0 ? 0 : Math.Round((double)answers.Count(a => a.Correct) / answers.Count, 2),
            MistakesLogged = mistakes.Count,
            MistakePatterns = mistakes
                .GroupBy(m => new { m.ReasonTag, m.SkillId })
                .Select(g => new MistakePatternDto
                {
                    Reason = g.Key.ReasonTag,
                    Count = g.Count(),
                    SkillId = g.Key.SkillId,
                    SkillName = skills.GetValueOrDefault(g.Key.SkillId)?.Name
                })
                .OrderByDescending(m => m.Count)
                .Take(8)
                .ToList(),
            ConfidenceChanges = confidence
                .GroupBy(c => c.SkillId)
                .Select(g =>
                {
                    var ordered = g.OrderBy(c => c.Date).ToList();
                    var from = ordered.First().Score;
                    var to = ordered.Last().Score;
                    return new ConfidenceChangeDto
                    {
                        SkillId = g.Key,
                        SkillName = skills.GetValueOrDefault(g.Key)?.Name ?? g.Key,
                        From = Math.Round(from, 2),
                        To = Math.Round(to, 2),
                        Change = Math.Round(to - from, 2)
                    };
                })
                .OrderByDescending(c => Math.Abs(c.Change))
                .ToList()
        };
    }

    public async Task<ProgressOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var today = AppClock.Today;
        var progress = await db.LessonProgress.AsNoTracking()
            .Where(p => p.Status == LessonStatus.Completed)
            .ToListAsync(ct);
        var answers = await db.AnswerRecords.AsNoTracking().ToListAsync(ct);
        var mistakes = await db.Mistakes.AsNoTracking().ToListAsync(ct);
        var skills = await db.Skills.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(ct);
        var lessons = await db.Lessons.AsNoTracking().ToListAsync(ct);
        var entries = await db.PlanEntries.AsNoTracking()
            .Where(e => e.Date >= today.AddDays(-13) && e.Status == PlanEntryStatus.Done)
            .ToListAsync(ct);
        var history = await db.ConfidenceHistory.AsNoTracking()
            .Where(c => c.Date >= today.AddDays(-60))
            .ToListAsync(ct);

        var skillProgress = new List<SkillProgressDto>();
        foreach (var skill in skills)
        {
            var skillLessonIds = lessons.Where(l => l.SkillId == skill.Id).Select(l => l.Id).ToHashSet();
            var done = progress.Count(p => skillLessonIds.Contains(p.LessonId));
            var latest = history.Where(c => c.SkillId == skill.Id).OrderByDescending(c => c.Date).FirstOrDefault();
            skillProgress.Add(new SkillProgressDto
            {
                SkillId = skill.Id,
                SkillName = skill.Name,
                LessonsDone = done,
                LessonsTotal = skillLessonIds.Count,
                Confidence = latest is null ? 0 : Math.Round(latest.Score, 2),
                History = history.Where(c => c.SkillId == skill.Id)
                    .OrderBy(c => c.Date)
                    .Select(c => new ConfidencePointDto { Date = Days.Iso(c.Date), Score = Math.Round(c.Score, 2) })
                    .ToList()
            });
        }

        return new ProgressOverviewDto
        {
            Streak = await GetStreakAsync(ct),
            ThisWeek = await GetWeekSummaryAsync(Days.StartOfWeek(today), ct),
            TotalLessonsDone = progress.Count,
            TotalMinutes = progress.Sum(p => lessons.FirstOrDefault(l => l.Id == p.LessonId)?.EstimatedMinutes ?? 0),
            OverallAccuracy = answers.Count == 0 ? 0 : Math.Round((double)answers.Count(a => a.Correct) / answers.Count, 2),
            TotalMistakes = mistakes.Count,
            MistakePatterns = mistakes
                .GroupBy(m => m.ReasonTag)
                .Select(g => new MistakePatternDto
                {
                    Reason = g.Key,
                    Count = g.Count(),
                    SkillId = g.First().SkillId,
                    SkillName = skills.FirstOrDefault(s => s.Id == g.First().SkillId)?.Name
                })
                .OrderByDescending(m => m.Count)
                .Take(12)
                .ToList(),
            Skills = skillProgress,
            Last14Days = Days.Range(today.AddDays(-13), today)
                .Select(d => new DailyMinutesDto
                {
                    Date = Days.Iso(d),
                    Minutes = entries.Where(e => e.Date == d).Sum(e => e.Minutes)
                })
                .ToList()
        };
    }

    public async Task<List<Mistake>> GetMistakesAsync(string? skillId, string? reason, CancellationToken ct = default)
    {
        var query = db.Mistakes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(skillId)) query = query.Where(m => m.SkillId == skillId);
        if (!string.IsNullOrWhiteSpace(reason)) query = query.Where(m => m.ReasonTag == reason);
        return await query.OrderByDescending(m => m.CreatedAt).Take(300).ToListAsync(ct);
    }

    public async Task<Mistake> UpdateMistakeAsync(
        int id,
        string reasonTag,
        string? note,
        CancellationToken ct = default)
    {
        var mistake = await db.Mistakes.FirstOrDefaultAsync(m => m.Id == id, ct)
                      ?? throw new KeyNotFoundException("Mistake not found.");
        mistake.ReasonTag = string.IsNullOrWhiteSpace(reasonTag) ? "Needs review" : reasonTag.Trim();
        mistake.Note = note;
        await db.SaveChangesAsync(ct);
        return mistake;
    }

    public async Task<Mistake> AddMistakeAsync(
        string skillId,
        string? lessonId,
        int? trackId,
        string reasonTag,
        string? note,
        CancellationToken ct = default)
    {
        if (!await db.Skills.AnyAsync(s => s.Id == skillId, ct))
            throw new ArgumentException("Unknown skill.");
        var mistake = new Mistake
        {
            SkillId = skillId,
            LessonId = lessonId,
            TrackId = trackId,
            Kind = MistakeKind.WrongAnswer,
            ReasonTag = string.IsNullOrWhiteSpace(reasonTag) ? "Needs review" : reasonTag.Trim(),
            Note = note
        };
        db.Mistakes.Add(mistake);
        await db.SaveChangesAsync(ct);
        return mistake;
    }
}
