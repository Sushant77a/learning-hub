using System.Text;
using LearningHub.Api.Data;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class IcsService(HubDbContext db, ScheduleService schedules)
{
    public async Task<string> BuildAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var entries = await db.PlanEntries.AsNoTracking()
            .Where(e => e.Date >= from && e.Date <= to && e.Status != PlanEntryStatus.Missed)
            .OrderBy(e => e.Date).ThenBy(e => e.Sequence)
            .ToListAsync(ct);
        var lessons = await db.Lessons.AsNoTracking().ToDictionaryAsync(l => l.Id, ct);
        var tracks = await db.Tracks.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
        var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var restDates = await db.RestDays.AsNoTracking()
            .Where(r => r.Date >= from && r.Date <= to)
            .OrderBy(r => r.Date)
            .ToListAsync(ct);
        var settings = await schedules.GetSettingsAsync(ct);
        var restWeekdays = ScheduleService.ParseRestWeekdays(settings.RestWeekdaysCsv);
        var activeTimeBox = await db.TimeBoxes.AsNoTracking()
            .Where(t => t.Status == TimeBoxStatus.Active)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var detourSkill = activeTimeBox is null
            ? null
            : skills.GetValueOrDefault(activeTimeBox.SkillId)?.Name;

        var builder = new StringBuilder();
        builder.AppendLine("BEGIN:VCALENDAR");
        builder.AppendLine("VERSION:2.0");
        builder.AppendLine("PRODID:-//Learning Hub//Study schedule//EN");
        builder.AppendLine("CALSCALE:GREGORIAN");
        builder.AppendLine("X-WR-CALNAME:Learning Hub");
        builder.AppendLine("METHOD:PUBLISH");

        foreach (var entry in entries)
        {
            var track = tracks.GetValueOrDefault(entry.TrackId);
            var skill = track is null ? "" : skills.GetValueOrDefault(track.SkillId)?.Name ?? track.SkillId;
            var lesson = lessons.GetValueOrDefault(entry.LessonId);
            var summary = $"{skill}: {lesson?.Title ?? entry.LessonId}";
            var description = new StringBuilder();
            description.Append($"{entry.Minutes} min study slice.");
            if (entry.Status == PlanEntryStatus.Done) description.Append(" Completed.");
            if (activeTimeBox?.TrackId == entry.TrackId && detourSkill is not null)
                description.Append($" {detourSkill} detour.");
            AppendEvent(builder, $"learninghub-entry-{entry.Id}@local", entry.Date, summary,
                description.ToString(), entry.Status == PlanEntryStatus.Done ? "CONFIRMED" : "PLANNED");
        }

        foreach (var rest in restDates)
        {
            AppendEvent(builder, $"learninghub-rest-{rest.Date:yyyyMMdd}@local", rest.Date, "Rest day",
                rest.Note ?? "Rest days are part of the plan.", "TENTATIVE", allDayOnly: true);
        }

        foreach (var day in Days.Range(from, to))
        {
            if (restDates.Any(r => r.Date == day)) continue;
            if (!restWeekdays.Contains(day.DayOfWeek)) continue;
            AppendEvent(builder, $"learninghub-rest-weekday-{day:yyyyMMdd}@local", day, "Rest day",
                "Recurring rest day.", "TENTATIVE", allDayOnly: true);
        }

        builder.AppendLine("END:VCALENDAR");
        return builder.ToString();
    }

    private static void AppendEvent(
        StringBuilder builder,
        string uid,
        DateOnly date,
        string summary,
        string description,
        string status,
        bool allDayOnly = false)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
        builder.AppendLine("BEGIN:VEVENT");
        builder.AppendLine($"UID:{uid}");
        builder.AppendLine($"DTSTAMP:{stamp}");
        builder.AppendLine($"DTSTART;VALUE=DATE:{date:yyyyMMdd}");
        builder.AppendLine($"DTEND;VALUE=DATE:{date.AddDays(1):yyyyMMdd}");
        builder.AppendLine($"SUMMARY:{Escape(summary)}");
        builder.AppendLine($"DESCRIPTION:{Escape(description)}");
        builder.AppendLine($"STATUS:{status}");
        builder.AppendLine("TRANSP:TRANSPARENT");
        builder.AppendLine("END:VEVENT");
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");
}
