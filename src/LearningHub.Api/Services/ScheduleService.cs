using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Core.Models;
using LearningHub.Core.Scheduling;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class ScheduleService(HubDbContext db, ILogger<ScheduleService> logger)
{
    public static readonly string[] WeekdayNames =
        Enum.GetNames<DayOfWeek>();

    public async Task<PlanSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await db.PlanSettings.FirstOrDefaultAsync(s => s.Id == 1, ct);
        if (settings is not null)
            return settings;

        settings = new PlanSettings
        {
            Id = 1,
            DailyMinutes = 90,
            RestWeekdaysCsv = "Saturday,Sunday",
            StartDate = AppClock.Today,
            BugFixFrequency = 25,
            UpdatedAt = DateTime.UtcNow
        };
        db.PlanSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }

    public static HashSet<DayOfWeek> ParseRestWeekdays(string csv)
    {
        var result = new HashSet<DayOfWeek>();
        foreach (var part in (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<DayOfWeek>(part, true, out var day))
                result.Add(day);
        }
        return result;
    }

    public static string SerializeRestWeekdays(IEnumerable<DayOfWeek> days)
        => string.Join(",", days.Distinct().OrderBy(d => (int)d).Select(d => d.ToString()));

    public async Task<PlanSnapshotDto> RebuildAsync(
        DateOnly today,
        PlanSimulation? simulation = null,
        bool persist = true,
        CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        var tracks = await db.Tracks.ToListAsync(ct);
        var pausedDsa = new HashSet<int>();
        var tracksWithDoneToday = await db.PlanEntries
            .Where(e => e.Date == today && e.Status == PlanEntryStatus.Done)
            .Select(e => e.TrackId)
            .Distinct()
            .ToListAsync(ct);

        var stale = await db.PlanEntries.Where(e => e.Status == PlanEntryStatus.Planned).ToListAsync(ct);
        var missedEntries = stale.Where(e => e.Date < today).ToList();
        var staleFuture = stale.Where(e => e.Date >= today).ToList();
        var missedDays = missedEntries.Select(e => e.Date).Distinct().Count();
        if (persist)
        {
            // Keep past uncompleted entries as history, never as overdue cards.
            foreach (var entry in missedEntries)
                entry.Status = PlanEntryStatus.Missed;
            if (staleFuture.Count > 0)
                db.PlanEntries.RemoveRange(staleFuture);
        }

        if (simulation?.SimulatedTrack is { } simulatedTrack)
        {
            simulatedTrack.State = TrackState.Active;
            tracks.Add(simulatedTrack);
        }

        var effectiveState = tracks.ToDictionary(t => t.Id, t => t.State);
        if (simulation is not null)
        {
            foreach (var id in simulation.PauseTrackIds)
                if (effectiveState.ContainsKey(id)) effectiveState[id] = TrackState.Paused;
            foreach (var id in simulation.ResumeTrackIds)
                if (effectiveState.ContainsKey(id)) effectiveState[id] = TrackState.Active;

            if (simulation.SimulatedTrack is { } st)
                effectiveState[st.Id] = TrackState.Active;
            foreach (var date in simulation.AddRestDates) pausedDsa.Add(-1); // marker only
        }

        var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var lessons = await db.Lessons.AsNoTracking().ToListAsync(ct);
        var lessonsBySkill = lessons
            .GroupBy(l => l.SkillId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(l => l.Order).ThenBy(l => l.Id).ToList());
        var progress = await db.LessonProgress.AsNoTracking().ToListAsync(ct);
        var completedByTrack = progress
            .Where(p => p.Status == LessonStatus.Completed)
            .GroupBy(p => p.TrackId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.LessonId).ToHashSet());
        var dsaCompleted = await db.CompletedDsaTopics.AsNoTracking().ToListAsync(ct);
        var completedTopicsByTrack = dsaCompleted
            .GroupBy(d => d.TrackId)
            .ToDictionary(g => g.Key, g => g.Select(d => d.Topic).ToHashSet());

        var activeTimeBox = simulation?.SimulatedTimeBox
                            ?? await db.TimeBoxes
                                .Where(t => t.Status == TimeBoxStatus.Active)
                                .OrderByDescending(t => t.CreatedAt)
                                .FirstOrDefaultAsync(ct);

        var restWeekdays = simulation?.RestWeekdaysCsv is not null
            ? ParseRestWeekdays(simulation.RestWeekdaysCsv)
            : ParseRestWeekdays(settings.RestWeekdaysCsv);
        var restDates = await db.RestDays.AsNoTracking().Select(r => r.Date).ToListAsync(ct);
        var restDateSet = restDates.ToHashSet();
        if (simulation is not null)
            foreach (var date in simulation.AddRestDates) restDateSet.Add(date);

        var dailyMinutes = Math.Clamp(simulation?.DailyMinutes ?? settings.DailyMinutes, 10, 24 * 60);
        var planStart = simulation?.StartDate ?? settings.StartDate;
        var startDate = planStart > today ? planStart : today;

        var scheduledTracks = tracks.Where(t => effectiveState[t.Id] == TrackState.Active).ToList();
        var pausedIds = new HashSet<int>();
        if (activeTimeBox is not null)
        {
            pausedIds = ParseIntList(activeTimeBox.PausedTrackIdsJson);
            foreach (var track in tracks.Where(t => pausedIds.Contains(t.Id)))
                if (!scheduledTracks.Contains(track)) scheduledTracks.Add(track);
        }

        var scheduleTracks = new List<ScheduleTrack>();
        var remainingByTrack = new Dictionary<int, List<Lesson>>();
        var names = new Dictionary<int, string>();
        foreach (var track in scheduledTracks)
        {
            if (!skills.TryGetValue(track.SkillId, out var skill)) continue;
            if (!lessonsBySkill.TryGetValue(track.SkillId, out var skillLessons) || skillLessons.Count == 0) continue;

            var completed = completedByTrack.GetValueOrDefault(track.Id, new HashSet<string>());
            var completedTopics = completedTopicsByTrack.GetValueOrDefault(track.Id, new HashSet<string>());
            var remaining = SelectRemainingLessons(skill, skillLessons, completed, completedTopics);
            if (remaining.Count == 0) continue;

            remainingByTrack[track.Id] = remaining;
            names[track.Id] = skill.Name;
            scheduleTracks.Add(new ScheduleTrack(
                track.Id,
                track.SkillId,
                skill.Name,
                remaining.Select(l => new ScheduleLesson(l.Id, l.EstimatedMinutes)).ToList()));
        }

        var trackStartDates = new Dictionary<int, DateOnly>();
        var trackEndDates = new Dictionary<int, DateOnly>();
        foreach (var track in scheduledTracks)
        {
            var start = tracksWithDoneToday.Contains(track.Id) ? today.AddDays(1) : startDate;
            if (pausedIds.Contains(track.Id)) start = today; // refined below
            trackStartDates[track.Id] = start;
        }

        var sharePhases = new List<SharePhase>();
        var scheduledIds = scheduleTracks.Select(t => t.TrackId).ToHashSet();
        if (activeTimeBox is not null && scheduledIds.Contains(activeTimeBox.TrackId))
        {
            var detourStart = activeTimeBox.StartDate > startDate ? activeTimeBox.StartDate : startDate;
            var effectiveEnd = activeTimeBox.EndDate < today ? today : activeTimeBox.EndDate;
            trackStartDates[activeTimeBox.TrackId] = detourStart;
            trackEndDates[activeTimeBox.TrackId] = effectiveEnd;

            var others = scheduleTracks
                .Where(t => t.TrackId != activeTimeBox.TrackId)
                .Select(t => t.TrackId)
                .ToList();

            if (activeTimeBox.Mode == TimeBoxMode.PauseOthers)
            {
                foreach (var other in others)
                    trackStartDates[other] = effectiveEnd.AddDays(1);

                sharePhases.Add(new SharePhase(detourStart, new Dictionary<int, int>
                {
                    [activeTimeBox.TrackId] = 100
                }));
                if (others.Count > 0)
                {
                    var equal = 100 / others.Count;
                    var shares = others.ToDictionary(id => id, id => equal);
                    shares[others[0]] += 100 - (equal * others.Count);
                    sharePhases.Add(new SharePhase(effectiveEnd.AddDays(1), shares));
                }
            }
            else
            {
                var detourPercent = Math.Clamp(activeTimeBox.DetourPercent, 5, 95);
                var shares = new Dictionary<int, int> { [activeTimeBox.TrackId] = detourPercent };
                if (others.Count > 0)
                {
                    var each = (100 - detourPercent) / others.Count;
                    foreach (var id in others) shares[id] = Math.Max(1, each);
                }
                sharePhases.Add(new SharePhase(detourStart, shares));

                var afterShares = others.Count > 0
                    ? others.ToDictionary(id => id, id => 100 / others.Count)
                    : new Dictionary<int, int>();
                if (afterShares.Count > 0)
                    sharePhases.Add(new SharePhase(effectiveEnd.AddDays(1), afterShares));
            }
        }

        var request = new ScheduleRequest
        {
            StartDate = startDate,
            DailyMinutes = dailyMinutes,
            Tracks = scheduleTracks,
            RestWeekdays = restWeekdays,
            RestDates = restDateSet,
            TrackStartDates = trackStartDates,
            TrackEndDates = trackEndDates,
            SharePhases = sharePhases,
            MaxLessonsPerTrackPerDay = 1
        };
        var result = Scheduler.Build(request);

        if (persist)
        {
            foreach (var entry in result.Entries.Where(e => e.TrackId > 0))
            {
                db.PlanEntries.Add(new PlanEntry
                {
                    TrackId = entry.TrackId,
                    LessonId = entry.LessonId,
                    Date = entry.Date,
                    Minutes = entry.Minutes,
                    Status = PlanEntryStatus.Planned,
                    Sequence = entry.Sequence,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync(ct);
        }

        var lessonById = lessons.ToDictionary(l => l.Id);
        var doneEntries = await db.PlanEntries
            .AsNoTracking()
            .Where(e => e.Status == PlanEntryStatus.Done && e.Date >= today.AddDays(-90))
            .ToListAsync(ct);

        var entriesDto = result.Entries.Select(e => new PlanItemDto
        {
            TrackId = e.TrackId,
            SkillId = e.SkillId,
            SkillName = names.GetValueOrDefault(e.TrackId, ""),
            LessonId = e.LessonId,
            LessonTitle = lessonById.GetValueOrDefault(e.LessonId)?.Title ?? e.LessonId,
            Date = Days.Iso(e.Date),
            Minutes = e.Minutes,
            Status = "planned",
            IsDetour = activeTimeBox?.TrackId == e.TrackId
        }).ToList();

        entriesDto.AddRange(doneEntries
            .Where(e => e.TrackId > 0)
            .Select(e => new PlanItemDto
            {
                TrackId = e.TrackId,
                SkillId = tracks.FirstOrDefault(t => t.Id == e.TrackId)?.SkillId ?? "",
                SkillName = skills.GetValueOrDefault(
                    tracks.FirstOrDefault(t => t.Id == e.TrackId)?.SkillId ?? "", new Skill()).Name,
                LessonId = e.LessonId,
                LessonTitle = lessonById.GetValueOrDefault(e.LessonId)?.Title ?? e.LessonId,
                Date = Days.Iso(e.Date),
                Minutes = e.Minutes,
                Status = "done",
                IsDetour = activeTimeBox?.TrackId == e.TrackId
            }));

        var todayShares = Scheduler.ResolveShares(request, scheduleTracks, today);
        var projectionByTrack = result.Projections.ToDictionary(p => p.TrackId);
        var firstDateByTrack = result.Entries
            .GroupBy(e => e.TrackId)
            .ToDictionary(g => g.Key, g => g.Min(e => e.Date));

        var trackDtos = new List<PlanTrackDto>();
        foreach (var track in tracks)
        {
            if (!skills.TryGetValue(track.SkillId, out var skill)) continue;
            var total = lessonsBySkill.GetValueOrDefault(track.SkillId, new List<Lesson>()).Count;
            var done = completedByTrack.GetValueOrDefault(track.Id, new HashSet<string>()).Count;
            projectionByTrack.TryGetValue(track.Id, out var projection);
            var state = effectiveState[track.Id];
            if (total > 0 && done >= total)
                state = TrackState.Completed;

            var isDetour = activeTimeBox?.TrackId == track.Id;
            trackDtos.Add(new PlanTrackDto
            {
                TrackId = track.Id,
                SkillId = track.SkillId,
                SkillName = skill.Name,
                State = state.ToString().ToLowerInvariant(),
                SharePercent = scheduledIds.Contains(track.Id) ? todayShares.GetValueOrDefault(track.Id) : 0,
                DailyMinutes = scheduledIds.Contains(track.Id)
                    ? (int)Math.Floor(dailyMinutes * todayShares.GetValueOrDefault(track.Id) / 100.0)
                    : 0,
                LessonsDone = done,
                LessonsTotal = total,
                FinishDate = projection?.FinishDate is { } finish ? Days.Iso(finish) : null,
                FirstLessonDate = firstDateByTrack.TryGetValue(track.Id, out var first) ? Days.Iso(first) : null,
                IsDetour = isDetour,
                ResumeOn = pausedIds.Contains(track.Id) && activeTimeBox is not null
                    ? Days.Iso((activeTimeBox.EndDate < today ? today : activeTimeBox.EndDate).AddDays(1))
                    : null
            });
        }

        var detourDto = BuildDetourDto(activeTimeBox, tracks, skills, today, pausedIds);

        return new PlanSnapshotDto
        {
            Today = Days.Iso(today),
            DailyMinutes = dailyMinutes,
            StartDate = Days.Iso(planStart),
            RestWeekdays = restWeekdays.Select(d => d.ToString()).ToList(),
            RestDates = restDateSet.OrderBy(d => d).Select(Days.Iso).ToList(),
            Entries = entriesDto.OrderBy(e => e.Date).ThenBy(e => e.TrackId).ToList(),
            Tracks = trackDtos.OrderBy(t => t.TrackId).ToList(),
            Detour = detourDto,
            TimeBoxes = detourDto is null ? new List<DetourDto>() : new List<DetourDto> { detourDto },
            MissedDaysShifted = missedDays,
            Changes = simulation?.Changes ?? new List<string>()
        };
    }

    private DetourDto? BuildDetourDto(
        TimeBox? timeBox,
        List<Track> tracks,
        Dictionary<string, Skill> skills,
        DateOnly today,
        HashSet<int> pausedIds)
    {
        if (timeBox is null) return null;
        var track = tracks.FirstOrDefault(t => t.Id == timeBox.TrackId);
        var skill = track is not null ? skills.GetValueOrDefault(track.SkillId) : null;
        var total = Math.Max(1, timeBox.EndDate.DayNumber - timeBox.StartDate.DayNumber + 1);
        var dayNumber = Math.Clamp(today.DayNumber - timeBox.StartDate.DayNumber + 1, 1, total);
        var effectiveEnd = timeBox.EndDate < today ? today : timeBox.EndDate;

        return new DetourDto
        {
            TimeBoxId = timeBox.Id,
            TrackId = timeBox.TrackId,
            SkillId = timeBox.SkillId,
            SkillName = skill?.Name ?? timeBox.SkillId,
            Label = timeBox.Label,
            StartDate = Days.Iso(timeBox.StartDate),
            EndDate = Days.Iso(timeBox.EndDate),
            DayNumber = dayNumber,
            TotalDays = total,
            Mode = timeBox.Mode == TimeBoxMode.PauseOthers ? "pause" : "parallel",
            DetourPercent = timeBox.DetourPercent,
            ResumeOn = Days.Iso(effectiveEnd.AddDays(1)),
            DecisionDue = timeBox.EndDate < today,
            PausedSkillNames = tracks
                .Where(t => pausedIds.Contains(t.Id))
                .Select(t => skills.GetValueOrDefault(t.SkillId)?.Name ?? t.SkillId)
                .ToList()
        };
    }

    public static List<Lesson> SelectRemainingLessons(
        Skill skill,
        List<Lesson> skillLessons,
        HashSet<string> completedLessons,
        HashSet<string> completedTopics)
    {
        if (!skill.IsDsa)
            return skillLessons.Where(l => !completedLessons.Contains(l.Id)).ToList();

        var topics = new List<string>();
        if (!string.IsNullOrWhiteSpace(skill.DsaTopicsJson))
        {
            try { topics = JsonSerializer.Deserialize<List<string>>(skill.DsaTopicsJson) ?? new(); }
            catch (JsonException) { topics = new(); }
        }

        // Lessons remain ordered topic by topic. Keeping later topics in the list means
        // the projected finish date is honest, while the order still guarantees that
        // the next topic is only reached after the current one is completed.
        // An override is expressed by marking a topic complete, which removes its
        // unfinished lessons from the sequence.
        return skillLessons
            .Where(l => !completedLessons.Contains(l.Id))
            .Where(l => l.Topic is null || !completedTopics.Contains(l.Topic))
            .OrderBy(l => topics.FindIndex(t => string.Equals(t, l.Topic, StringComparison.OrdinalIgnoreCase)) is var index && index >= 0 ? index : int.MaxValue)
            .ThenBy(l => l.Order)
            .ToList();
    }

    public static HashSet<int> ParseIntList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new HashSet<int>();
        try { return (JsonSerializer.Deserialize<List<int>>(json) ?? new()).ToHashSet(); }
        catch (JsonException) { return new HashSet<int>(); }
    }
}
