using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class PlanChangeRequest
{
    public int? DailyMinutes { get; set; }
    public string? RestWeekdays { get; set; }
    public string? StartDate { get; set; }
    public List<string>? RestDates { get; set; }
    public int? PauseTrackId { get; set; }
    public int? ResumeTrackId { get; set; }
    public string? SkipDate { get; set; }
}

public sealed class TimeBoxRequest
{
    public string SkillId { get; set; } = "";
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public int? Days { get; set; }
    public string Mode { get; set; } = "pause";
    public int DetourPercent { get; set; } = 50;
    public string? Label { get; set; }
}

public sealed class TimeBoxResult
{
    public PlanSnapshotDto Plan { get; set; } = new();
    public int TrackId { get; set; }
    public int TimeBoxId { get; set; }
    public bool NeedsCurriculum { get; set; }
    public string Message { get; set; } = "";
}

public sealed class WorkspaceService(HubDbContext db, ScheduleService schedules, AiService ai)
{
    public async Task<PlanSnapshotDto> PreviewPlanAsync(PlanChangeRequest request, CancellationToken ct = default)
    {
        var today = AppClock.Today;
        var simulation = new PlanSimulation
        {
            DailyMinutes = ValidateMinutes(request.DailyMinutes),
            RestWeekdaysCsv = NormalizeWeekdays(request.RestWeekdays),
            StartDate = ParseDate(request.StartDate),
        };
        DescribeChanges(simulation, request);

        if (request.PauseTrackId is { } pause)
        {
            await EnsureTrackAsync(pause, ct);
            simulation.PauseTrackIds.Add(pause);
            simulation.Changes.Add("Pause the track and shift its lessons forward.");
        }
        if (request.ResumeTrackId is { } resume)
        {
            await EnsureTrackAsync(resume, ct);
            simulation.ResumeTrackIds.Add(resume);
            simulation.Changes.Add("Resume the track and reflow the plan.");
        }
        foreach (var dateText in request.RestDates ?? new List<string>())
            if (ParseDate(dateText) is { } date) simulation.AddRestDates.Add(date);
        if (ParseDate(request.SkipDate) is { } skip)
        {
            simulation.AddRestDates.Add(skip);
            simulation.Changes.Add($"Mark {skip:d MMM} as a rest day and reflow.");
        }

        return await schedules.RebuildAsync(today, simulation, persist: false, ct);
    }

    public async Task<PlanSnapshotDto> ApplyPlanAsync(PlanChangeRequest request, CancellationToken ct = default)
    {
        var settings = await schedules.GetSettingsAsync(ct);
        if (ValidateMinutes(request.DailyMinutes) is { } minutes) settings.DailyMinutes = minutes;
        if (NormalizeWeekdays(request.RestWeekdays) is { } weekdays) settings.RestWeekdaysCsv = weekdays;
        if (ParseDate(request.StartDate) is { } start) settings.StartDate = start;
        settings.UpdatedAt = DateTime.UtcNow;

        foreach (var dateText in request.RestDates ?? new List<string>())
            if (ParseDate(dateText) is { } date) await AddRestDateCoreAsync(date, null, ct);
        if (ParseDate(request.SkipDate) is { } skip)
            await AddRestDateCoreAsync(skip, "Skipped day", ct);

        if (request.PauseTrackId is { } pause)
        {
            var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == pause, ct)
                        ?? throw new KeyNotFoundException("Track not found.");
            track.State = TrackState.Paused;
            track.PausedReason = "Paused by you";
        }
        if (request.ResumeTrackId is { } resume)
        {
            var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == resume, ct)
                        ?? throw new KeyNotFoundException("Track not found.");
            track.State = TrackState.Active;
            track.PausedReason = null;
            track.StartedAt ??= DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return await schedules.RebuildAsync(AppClock.Today, ct: ct);
    }

    public async Task<PlanSnapshotDto> PreviewNewTrackAsync(string skillId, CancellationToken ct = default)
    {
        var skill = await db.Skills.AsNoTracking().FirstOrDefaultAsync(s => s.Id == skillId, ct)
                    ?? throw new KeyNotFoundException("Skill not found.");
        var existing = await db.Tracks.FirstOrDefaultAsync(t => t.SkillId == skillId, ct);
        var simulation = new PlanSimulation();
        if (existing is null)
        {
            simulation.SimulatedTrack = new Track
            {
                Id = -1000,
                SkillId = skillId,
                State = TrackState.Active,
                CreatedAt = DateTime.UtcNow
            };
            simulation.Changes.Add($"Start studying {skill.Name} and reflow the plan.");
        }
        else
        {
            simulation.ResumeTrackIds.Add(existing.Id);
            simulation.Changes.Add($"Resume {skill.Name} and reflow the plan.");
        }
        return await schedules.RebuildAsync(AppClock.Today, simulation, persist: false, ct);
    }

    public async Task<PlanSnapshotDto> StartTrackAsync(string skillId, CancellationToken ct = default)
    {
        if (!await db.Skills.AnyAsync(s => s.Id == skillId, ct))
            throw new KeyNotFoundException("Skill not found.");
        var track = await db.Tracks.FirstOrDefaultAsync(t => t.SkillId == skillId, ct);
        if (track is null)
        {
            track = new Track
            {
                SkillId = skillId,
                State = TrackState.Active,
                CreatedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow
            };
            db.Tracks.Add(track);
        }
        else
        {
            track.State = TrackState.Active;
            track.PausedReason = null;
            track.CompletedAt = null;
            track.StartedAt ??= DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return await schedules.RebuildAsync(AppClock.Today, ct: ct);
    }

    public async Task<PlanSnapshotDto> PreviewTrackStateAsync(int trackId, string state, CancellationToken ct = default)
    {
        await EnsureTrackAsync(trackId, ct);
        var normalized = NormalizeState(state);
        var simulation = new PlanSimulation();
        if (normalized == "paused") simulation.PauseTrackIds.Add(trackId);
        else simulation.ResumeTrackIds.Add(trackId);
        simulation.Changes.Add(normalized == "paused"
            ? "Pause this track and shift its lessons forward."
            : "Resume this track and reflow the plan.");
        return await schedules.RebuildAsync(AppClock.Today, simulation, persist: false, ct);
    }

    public async Task<PlanSnapshotDto> SetTrackStateAsync(int trackId, string state, CancellationToken ct = default)
    {
        var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == trackId, ct)
                    ?? throw new KeyNotFoundException("Track not found.");
        var normalized = NormalizeState(state);
        if (normalized == "paused")
        {
            track.State = TrackState.Paused;
            track.PausedReason = "Paused by you";
        }
        else
        {
            track.State = TrackState.Active;
            track.PausedReason = null;
            track.CompletedAt = null;
            track.StartedAt ??= DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return await schedules.RebuildAsync(AppClock.Today, ct: ct);
    }

    public async Task<(PlanSnapshotDto Plan, string Topic)> CompleteDsaTopicAsync(
        int trackId,
        string? topic,
        CancellationToken ct = default)
    {
        var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == trackId, ct)
                    ?? throw new KeyNotFoundException("Track not found.");
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == track.SkillId, ct)
                    ?? throw new KeyNotFoundException("Skill not found.");
        if (!skill.IsDsa) throw new ArgumentException("This track is not a DSA track.");

        var lessons = await db.Lessons.AsNoTracking().Where(l => l.SkillId == skill.Id).OrderBy(l => l.Order).ToListAsync(ct);
        var completed = await db.LessonProgress.AsNoTracking()
            .Where(p => p.TrackId == trackId && p.Status == LessonStatus.Completed)
            .Select(p => p.LessonId).ToListAsync(ct);
        var completedTopics = await db.CompletedDsaTopics.AsNoTracking()
            .Where(t => t.TrackId == trackId).Select(t => t.Topic).ToListAsync(ct);
        var current = ScheduleService.SelectRemainingLessons(
            skill, lessons, completed.ToHashSet(), completedTopics.ToHashSet()).FirstOrDefault()?.Topic;
        var target = string.IsNullOrWhiteSpace(topic) ? current : topic;
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("There is no current DSA topic to complete.");

        if (!await db.CompletedDsaTopics.AnyAsync(t => t.TrackId == trackId && t.Topic == target, ct))
        {
            db.CompletedDsaTopics.Add(new CompletedDsaTopic { TrackId = trackId, Topic = target });
            track.DsaOverride = true;
            await db.SaveChangesAsync(ct);
        }

        return (await schedules.RebuildAsync(AppClock.Today, ct: ct), target);
    }

    public async Task<PlanSnapshotDto> PreviewTimeBoxAsync(TimeBoxRequest request, CancellationToken ct = default)
    {
        var (skill, track, start, end) = await ResolveTimeBoxAsync(request, ct);
        var simulation = BuildTimeBoxSimulation(skill, track, start, end, request);
        return await schedules.RebuildAsync(AppClock.Today, simulation, persist: false, ct);
    }

    public async Task<TimeBoxResult> ApplyTimeBoxAsync(TimeBoxRequest request, CancellationToken ct = default)
    {
        if (await db.TimeBoxes.AnyAsync(t => t.Status == TimeBoxStatus.Active, ct))
            throw new InvalidOperationException("A detour is already running. End or extend it before starting another.");

        var (skill, track, start, end) = await ResolveTimeBoxAsync(request, ct);
        if (track is null)
        {
            track = new Track
            {
                SkillId = skill.Id,
                State = TrackState.Active,
                CreatedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow
            };
            db.Tracks.Add(track);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            track.State = TrackState.Active;
            track.PausedReason = null;
            track.CompletedAt = null;
            track.StartedAt ??= DateTime.UtcNow;
        }

        var paused = new List<int>();
        if (request.Mode.Equals("pause", StringComparison.OrdinalIgnoreCase))
        {
            var others = await db.Tracks
                .Where(t => t.State == TrackState.Active && t.Id != track.Id)
                .ToListAsync(ct);
            foreach (var other in others)
            {
                other.State = TrackState.Paused;
                other.PausedReason = $"{skill.Name} detour";
                paused.Add(other.Id);
            }
        }

        var timeBox = new TimeBox
        {
            SkillId = skill.Id,
            TrackId = track.Id,
            StartDate = start,
            EndDate = end,
            Mode = request.Mode.Equals("parallel", StringComparison.OrdinalIgnoreCase)
                ? TimeBoxMode.Parallel
                : TimeBoxMode.PauseOthers,
            DetourPercent = Math.Clamp(request.DetourPercent, 5, 95),
            Status = TimeBoxStatus.Active,
            PausedTrackIdsJson = JsonSerializer.Serialize(paused),
            Label = string.IsNullOrWhiteSpace(request.Label) ? $"{skill.Name} detour" : request.Label.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.TimeBoxes.Add(timeBox);
        await db.SaveChangesAsync(ct);

        var plan = await schedules.RebuildAsync(AppClock.Today, ct: ct);
        var needsCurriculum = !await db.Lessons.AnyAsync(l => l.SkillId == skill.Id, ct);
        return new TimeBoxResult
        {
            Plan = plan,
            TrackId = track.Id,
            TimeBoxId = timeBox.Id,
            NeedsCurriculum = needsCurriculum,
            Message = needsCurriculum
                ? "No lessons yet. Generate a mini-curriculum sized to this time box."
                : $"{skill.Name} detour started."
        };
    }

    public async Task<PlanSnapshotDto> EndTimeBoxAsync(int id, string choice, string? extendTo, CancellationToken ct = default)
    {
        var timeBox = await db.TimeBoxes.FirstOrDefaultAsync(t => t.Id == id, ct)
                      ?? throw new KeyNotFoundException("Detour not found.");
        if (timeBox.Status != TimeBoxStatus.Active)
            return await schedules.RebuildAsync(AppClock.Today, ct: ct);

        if (choice.Equals("extend", StringComparison.OrdinalIgnoreCase))
        {
            var newEnd = ParseDate(extendTo) ?? throw new ArgumentException("A new end date is required.");
            if (newEnd <= timeBox.EndDate)
                throw new ArgumentException("The new end date must be after the current end date.");
            timeBox.EndDate = newEnd;
            await db.SaveChangesAsync(ct);
            return await schedules.RebuildAsync(AppClock.Today, ct: ct);
        }

        var pausedIds = ScheduleService.ParseIntList(timeBox.PausedTrackIdsJson);
        var pausedTracks = await db.Tracks.Where(t => pausedIds.Contains(t.Id)).ToListAsync(ct);
        var detourTrack = await db.Tracks.FirstOrDefaultAsync(t => t.Id == timeBox.TrackId, ct);

        if (choice.Equals("keepBoth", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var track in pausedTracks)
            {
                track.State = TrackState.Active;
                track.PausedReason = null;
            }
            if (detourTrack is not null)
            {
                detourTrack.State = TrackState.Active;
                detourTrack.PausedReason = null;
            }
        }
        else
        {
            foreach (var track in pausedTracks)
            {
                track.State = TrackState.Active;
                track.PausedReason = null;
            }
            if (detourTrack is not null)
            {
                detourTrack.State = TrackState.Paused;
                detourTrack.PausedReason = "Detour ended — resume when you want";
            }
        }

        timeBox.Status = TimeBoxStatus.Ended;
        await db.SaveChangesAsync(ct);
        return await schedules.RebuildAsync(AppClock.Today, ct: ct);
    }

    public async Task<PlanSnapshotDto> AddRestDayAsync(DateOnly date, string? note, CancellationToken ct = default)
    {
        await AddRestDateCoreAsync(date, note, ct);
        await db.SaveChangesAsync(ct);
        return await schedules.RebuildAsync(AppClock.Today, ct: ct);
    }

    public async Task<PlanSnapshotDto> RemoveRestDayAsync(DateOnly date, CancellationToken ct = default)
    {
        var rest = await db.RestDays.FirstOrDefaultAsync(r => r.Date == date, ct);
        if (rest is not null)
        {
            db.RestDays.Remove(rest);
            await db.SaveChangesAsync(ct);
        }
        return await schedules.RebuildAsync(AppClock.Today, ct: ct);
    }

    private async Task AddRestDateCoreAsync(DateOnly date, string? note, CancellationToken ct)
    {
        var existing = await db.RestDays.FirstOrDefaultAsync(r => r.Date == date, ct);
        if (existing is null)
            db.RestDays.Add(new RestDay { Date = date, Note = note });
        else if (!string.IsNullOrWhiteSpace(note))
            existing.Note = note;
    }

    private async Task EnsureTrackAsync(int trackId, CancellationToken ct)
    {
        if (!await db.Tracks.AnyAsync(t => t.Id == trackId, ct))
            throw new KeyNotFoundException("Track not found.");
    }

    private static string NormalizeState(string state) => state.ToLowerInvariant() switch
    {
        "paused" => "paused",
        "active" or "resume" => "active",
        _ => throw new ArgumentException("State must be active or paused.")
    };

    private static int? ValidateMinutes(int? minutes)
    {
        if (minutes is null) return null;
        if (minutes < 15 || minutes > 720) throw new ArgumentException("Daily minutes must be between 15 and 720.");
        return minutes;
    }

    private static string? NormalizeWeekdays(string? csv)
    {
        if (csv is null) return null;
        var days = ScheduleService.ParseRestWeekdays(csv);
        return ScheduleService.SerializeRestWeekdays(days);
    }

    public static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParse(value, out var date)
            ? date
            : throw new ArgumentException($"'{value}' is not a valid date.");
    }

    private async Task<(Skill Skill, Track? Track, DateOnly Start, DateOnly End)> ResolveTimeBoxAsync(
        TimeBoxRequest request,
        CancellationToken ct)
    {
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == request.SkillId, ct)
                    ?? throw new KeyNotFoundException("Skill not found.");
        var track = await db.Tracks.Where(t => t.SkillId == skill.Id)
            .OrderBy(t => t.State == TrackState.Active ? 0 : 1)
            .ThenByDescending(t => t.Id)
            .FirstOrDefaultAsync(ct);

        var today = AppClock.Today;
        var start = ParseDate(request.StartDate) ?? today;
        var end = ParseDate(request.EndDate)
                  ?? (request.Days is { } days && days > 0
                      ? start.AddDays(Math.Clamp(days, 1, 60) - 1)
                      : start.AddDays(4));
        if (request.Days is { } d2 && d2 > 0 && request.EndDate is null)
            end = start.AddDays(Math.Clamp(d2, 1, 60) - 1);
        if (end < start)
            throw new ArgumentException("The time box end date must be on or after its start date.");
        if (end.DayNumber - start.DayNumber + 1 > 60)
            throw new ArgumentException("A time box can be at most 60 days.");

        return (skill, track, start, end);
    }

    private PlanSimulation BuildTimeBoxSimulation(
        Skill skill,
        Track? track,
        DateOnly start,
        DateOnly end,
        TimeBoxRequest request)
    {
        var simulation = new PlanSimulation();
        var mode = request.Mode.Equals("parallel", StringComparison.OrdinalIgnoreCase)
            ? TimeBoxMode.Parallel
            : TimeBoxMode.PauseOthers;

        Track simulatedTrack;
        if (track is null)
        {
            simulatedTrack = new Track
            {
                Id = -1000,
                SkillId = skill.Id,
                State = TrackState.Active,
                CreatedAt = DateTime.UtcNow
            };
            simulation.SimulatedTrack = simulatedTrack;
        }
        else
        {
            simulatedTrack = track;
            simulation.ResumeTrackIds.Add(track.Id);
        }

        var paused = new List<int>();
        if (mode == TimeBoxMode.PauseOthers)
        {
            foreach (var active in db.Tracks.Local
                         .Where(t => t.State == TrackState.Active && t.Id != simulatedTrack.Id)
                         .Select(t => t.Id)
                         .Concat(db.Tracks.AsNoTracking()
                             .Where(t => t.State == TrackState.Active && t.Id != simulatedTrack.Id)
                             .Select(t => t.Id))
                         .Distinct())
            {
                simulation.PauseTrackIds.Add(active);
                paused.Add(active);
            }
        }

        simulation.SimulatedTimeBox = new TimeBox
        {
            Id = 0,
            SkillId = skill.Id,
            TrackId = simulatedTrack.Id,
            StartDate = start,
            EndDate = end,
            Mode = mode,
            DetourPercent = Math.Clamp(request.DetourPercent, 5, 95),
            Status = TimeBoxStatus.Active,
            PausedTrackIdsJson = JsonSerializer.Serialize(paused),
            Label = string.IsNullOrWhiteSpace(request.Label) ? $"{skill.Name} detour" : request.Label.Trim()
        };
        simulation.Changes.Add(mode == TimeBoxMode.Parallel
            ? $"Run {skill.Name} in parallel at {Math.Clamp(request.DetourPercent, 5, 95)}% of daily time until {end:d MMM}."
            : $"Pause other tracks for a {skill.Name} detour until {end:d MMM}.");
        return simulation;
    }

    private void DescribeChanges(PlanSimulation simulation, PlanChangeRequest request)
    {
        if (request.DailyMinutes is not null)
            simulation.Changes.Add($"Daily study time becomes {request.DailyMinutes} minutes.");
        if (request.RestWeekdays is not null)
            simulation.Changes.Add("Rest weekdays change and the plan reflows.");
        if (request.StartDate is not null)
            simulation.Changes.Add($"Plan start moves to {request.StartDate}.");
    }
}
