namespace LearningHub.Core.Scheduling;

public sealed record ScheduleLesson(string LessonId, int EstimatedMinutes);

public sealed record ScheduleTrack(
    int TrackId,
    string SkillId,
    string Name,
    IReadOnlyList<ScheduleLesson> Lessons);

public sealed record SharePhase(DateOnly From, IReadOnlyDictionary<int, int> Shares);

public sealed record ScheduleRequest
{
    public required DateOnly StartDate { get; init; }
    public required int DailyMinutes { get; init; }
    public required IReadOnlyList<ScheduleTrack> Tracks { get; init; }
    public IReadOnlySet<DayOfWeek> RestWeekdays { get; init; } = new HashSet<DayOfWeek>();
    public IReadOnlySet<DateOnly> RestDates { get; init; } = new HashSet<DateOnly>();

    /// <summary>Default explicit allocation (percentages, normalised). Null shares the day equally.</summary>
    public IReadOnlyDictionary<int, int>? TrackShares { get; init; }

    /// <summary>Optional time-phased allocations; the last phase whose From &lt;= date wins.</summary>
    public IReadOnlyList<SharePhase> SharePhases { get; init; } = Array.Empty<SharePhase>();

    /// <summary>Lets a track start later (for example it already studied today, or resumes after a detour).</summary>
    public IReadOnlyDictionary<int, DateOnly> TrackStartDates { get; init; } = new Dictionary<int, DateOnly>();

    /// <summary>Lets a track stop early (for example a time-boxed detour ending).</summary>
    public IReadOnlyDictionary<int, DateOnly> TrackEndDates { get; init; } = new Dictionary<int, DateOnly>();

    public int HorizonDays { get; init; } = 3650;
    public int MaxLessonsPerTrackPerDay { get; init; } = 1;
}

public sealed record ScheduledEntry(
    int TrackId,
    string SkillId,
    string LessonId,
    DateOnly Date,
    int Minutes,
    int Sequence);

public sealed record TrackProjection(
    int TrackId,
    string SkillId,
    DateOnly? FinishDate,
    int LessonsScheduled,
    int TotalMinutes,
    int DailyMinutes);

public sealed record ScheduleResult(
    IReadOnlyList<ScheduledEntry> Entries,
    IReadOnlyList<TrackProjection> Projections,
    IReadOnlyList<DateOnly> RestDatesInWindow);

/// <summary>
/// Pure forward scheduler. It never produces past entries: callers rebuild from today,
/// which is how missed days shift the plan forward instead of piling up as overdue.
/// </summary>
public static class Scheduler
{
    public static ScheduleResult Build(ScheduleRequest request)
    {
        if (request.DailyMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Daily minutes must be positive.");

        var tracks = request.Tracks.Where(t => t.Lessons.Count > 0).ToList();
        var entries = new List<ScheduledEntry>();
        var restDates = new List<DateOnly>();
        var nextIndex = tracks.ToDictionary(t => t.TrackId, _ => 0);
        var scheduledCount = tracks.ToDictionary(t => t.TrackId, _ => 0);
        var totalMinutes = tracks.ToDictionary(t => t.TrackId, _ => 0);
        var lastDate = tracks.ToDictionary(t => t.TrackId, _ => (DateOnly?)null);
        var lastAllocation = tracks.ToDictionary(t => t.TrackId, _ => 0);

        var date = request.StartDate;
        for (var day = 0; day < request.HorizonDays; day++, date = date.AddDays(1))
        {
            if (request.RestWeekdays.Contains(date.DayOfWeek))
                continue;

            if (request.RestDates.Contains(date))
            {
                restDates.Add(date);
                continue;
            }

            var shares = ResolveShares(request, tracks, date);
            var allocations = tracks.ToDictionary(
                t => t.TrackId,
                t => shares[t.TrackId] <= 0
                    ? 0
                    : Math.Max(5, (int)Math.Floor(request.DailyMinutes * shares[t.TrackId] / 100.0)));

            var allDone = true;
            foreach (var track in tracks)
            {
                var index = nextIndex[track.TrackId];
                if (index >= track.Lessons.Count)
                    continue;

                if (request.TrackStartDates.TryGetValue(track.TrackId, out var notBefore) && date < notBefore)
                {
                    allDone = false;
                    continue;
                }

                if (request.TrackEndDates.TryGetValue(track.TrackId, out var notAfter) && date > notAfter)
                {
                    allDone = false;
                    continue;
                }

                allDone = false;
                lastAllocation[track.TrackId] = allocations[track.TrackId];
                var slot = allocations[track.TrackId];
                var lessonsToday = 0;
                while (index < track.Lessons.Count
                       && lessonsToday < request.MaxLessonsPerTrackPerDay
                       && slot >= 5)
                {
                    var lesson = track.Lessons[index];
                    var minutes = Math.Min(slot, Math.Max(5, lesson.EstimatedMinutes));
                    entries.Add(new ScheduledEntry(
                        track.TrackId,
                        track.SkillId,
                        lesson.LessonId,
                        date,
                        minutes,
                        entries.Count));
                    slot -= minutes;
                    totalMinutes[track.TrackId] += minutes;
                    scheduledCount[track.TrackId]++;
                    lastDate[track.TrackId] = date;
                    index++;
                    lessonsToday++;
                }

                nextIndex[track.TrackId] = index;
            }

            if (allDone)
                break;
        }

        var projections = tracks.Select(t => new TrackProjection(
                t.TrackId,
                t.SkillId,
                lastDate[t.TrackId],
                scheduledCount[t.TrackId],
                totalMinutes[t.TrackId],
                lastAllocation[t.TrackId]))
            .ToList();

        return new ScheduleResult(entries, projections, restDates);
    }

    /// <summary>Exposed for UI and tests: effective per-track percentages on a given day.</summary>
    public static Dictionary<int, int> ResolveShares(
        ScheduleRequest request,
        IReadOnlyList<ScheduleTrack> tracks,
        DateOnly date)
    {
        var phase = request.SharePhases
            .Where(p => p.From <= date)
            .OrderByDescending(p => p.From)
            .FirstOrDefault();

        var requested = phase?.Shares ?? request.TrackShares;
        return NormaliseShares(tracks, requested);
    }

    private static Dictionary<int, int> NormaliseShares(
        IReadOnlyList<ScheduleTrack> tracks,
        IReadOnlyDictionary<int, int>? requested)
    {
        var result = new Dictionary<int, int>();
        if (tracks.Count == 0)
            return result;

        if (requested is null || requested.Count == 0)
        {
            var equal = 100 / tracks.Count;
            foreach (var t in tracks)
                result[t.TrackId] = equal;
            result[tracks[0].TrackId] += 100 - (equal * tracks.Count);
            return result;
        }

        var values = tracks.ToDictionary(
            t => t.TrackId,
            t => requested.TryGetValue(t.TrackId, out var p) ? Math.Max(0, p) : 0);
        var sum = values.Values.Sum();
        if (sum <= 0)
        {
            var equal = 100 / tracks.Count;
            foreach (var t in tracks)
                result[t.TrackId] = equal;
            if (tracks.Count > 0)
                result[tracks[0].TrackId] += 100 - (equal * tracks.Count);
            return result;
        }

        var exact = values.ToDictionary(kv => kv.Key, kv => kv.Value * 100.0 / sum);
        var floors = exact.ToDictionary(kv => kv.Key, kv => (int)Math.Floor(kv.Value));
        var remainder = 100 - floors.Values.Sum();
        foreach (var kv in exact
                     .Where(kv => values[kv.Key] > 0)
                     .OrderByDescending(kv => kv.Value - Math.Floor(kv.Value))
                     .ThenBy(kv => kv.Key)
                     .Take(Math.Max(0, remainder)))
        {
            floors[kv.Key]++;
        }

        foreach (var t in tracks)
            result[t.TrackId] = floors[t.TrackId];

        return result;
    }
}
