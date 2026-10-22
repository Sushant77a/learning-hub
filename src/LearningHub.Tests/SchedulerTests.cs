using LearningHub.Core.Scheduling;

namespace LearningHub.Tests;

public class SchedulerTests
{
    private static readonly DateOnly Monday = new(2026, 10, 19);

    private static ScheduleTrack Track(int id, string skill, params (string Id, int Minutes)[] lessons)
        => new(id, skill, skill, lessons.Select(l => new ScheduleLesson(l.Id, l.Minutes)).ToList());

    private static ScheduleRequest Request(
        DateOnly start,
        int daily,
        IEnumerable<ScheduleTrack> tracks,
        IReadOnlySet<DayOfWeek>? restWeekdays = null,
        IReadOnlySet<DateOnly>? restDates = null,
        IReadOnlyDictionary<int, int>? shares = null,
        IReadOnlyList<SharePhase>? phases = null,
        IReadOnlyDictionary<int, DateOnly>? startDates = null,
        IReadOnlyDictionary<int, DateOnly>? endDates = null)
        => new()
        {
            StartDate = start,
            DailyMinutes = daily,
            Tracks = tracks.ToList(),
            RestWeekdays = restWeekdays ?? new HashSet<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday },
            RestDates = restDates ?? new HashSet<DateOnly>(),
            TrackShares = shares,
            SharePhases = phases ?? Array.Empty<SharePhase>(),
            TrackStartDates = startDates ?? new Dictionary<int, DateOnly>(),
            TrackEndDates = endDates ?? new Dictionary<int, DateOnly>()
        };

    [Fact]
    public void SchedulesOneLessonPerStudyDay_AndSkipsWeekends()
    {
        var track = Track(1, "csharp",
            ("l1", 30), ("l2", 30), ("l3", 30), ("l4", 30));

        var result = Scheduler.Build(Request(Monday, 60, new[] { track }));

        Assert.Equal(4, result.Entries.Count);
        Assert.Equal(new[] { Monday, Monday.AddDays(1), Monday.AddDays(2), Monday.AddDays(3) },
            result.Entries.Select(e => e.Date).ToArray());
        Assert.Equal(Monday.AddDays(3), result.Projections.Single().FinishDate);
        Assert.Equal(60, result.Projections.Single().DailyMinutes);
    }

    [Fact]
    public void SkipsExplicitRestDates_AndDoesNotScheduleOnRecurringRestWeekdays()
    {
        var track = Track(1, "csharp", ("l1", 30), ("l2", 30), ("l3", 30));
        var restDates = new HashSet<DateOnly> { Monday.AddDays(1) };

        var result = Scheduler.Build(Request(Monday, 60, new[] { track }, restDates: restDates));

        Assert.DoesNotContain(result.Entries, e => e.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.DoesNotContain(result.Entries, e => e.Date == Monday.AddDays(1));
        Assert.Contains(Monday.AddDays(1), result.RestDatesInWindow);
        Assert.Equal(Monday.AddDays(3), result.Projections.Single().FinishDate);
    }

    [Fact]
    public void SharesTheDayEquallyBetweenTracks()
    {
        var csharp = Track(1, "csharp", ("c1", 60), ("c2", 60));
        var dsa = Track(2, "dsa", ("d1", 60), ("d2", 60));

        var result = Scheduler.Build(Request(Monday, 120, new[] { csharp, dsa }));

        Assert.Equal(60, result.Projections.Single(p => p.TrackId == 1).DailyMinutes);
        Assert.Equal(60, result.Projections.Single(p => p.TrackId == 2).DailyMinutes);
        var firstDay = result.Entries.Where(e => e.Date == Monday).ToList();
        Assert.Equal(2, firstDay.Count);
        Assert.All(firstDay, e => Assert.Equal(60, e.Minutes)); // each track has a 60 min slice and a 60 min lesson
        Assert.Equal(Monday.AddDays(1), result.Projections.Single(p => p.TrackId == 1).FinishDate);
    }

    [Fact]
    public void HonoursExplicitTimeSplit()
    {
        var csharp = Track(1, "csharp", ("c1", 120), ("c2", 120));
        var dsa = Track(2, "dsa", ("d1", 120), ("d2", 120));
        var shares = new Dictionary<int, int> { [1] = 60, [2] = 40 };

        var result = Scheduler.Build(Request(Monday, 100, new[] { csharp, dsa }, shares: shares));

        Assert.Equal(60, result.Entries.First(e => e.TrackId == 1).Minutes);
        Assert.Equal(40, result.Entries.First(e => e.TrackId == 2).Minutes);
    }

    [Fact]
    public void PausesOneTrackDuringADetour_ThenResumesItAfterwards()
    {
        var main = Track(1, "dsa", ("a1", 30), ("a2", 30), ("a3", 30));
        var detour = Track(2, "nginx", ("n1", 30), ("n2", 30));
        var resume = Monday.AddDays(7); // after the weekend
        var phases = new List<SharePhase>
        {
            new(Monday, new Dictionary<int, int> { [2] = 100 }),
            new(resume, new Dictionary<int, int> { [1] = 100 })
        };
        var startDates = new Dictionary<int, DateOnly>
        {
            [1] = resume,
            [2] = Monday
        };
        var endDates = new Dictionary<int, DateOnly> { [2] = Monday.AddDays(4) };

        var result = Scheduler.Build(Request(Monday, 60, new[] { main, detour }, phases: phases,
            startDates: startDates, endDates: endDates));

        Assert.Equal(2, result.Entries.Count(e => e.TrackId == 2));
        Assert.All(result.Entries.Where(e => e.TrackId == 2), e => Assert.True(e.Date < resume));
        Assert.All(result.Entries.Where(e => e.TrackId == 1), e => Assert.True(e.Date >= resume));
        Assert.Equal(3, result.Entries.Count(e => e.TrackId == 1));
        Assert.Equal(Monday.AddDays(9), result.Projections.Single(p => p.TrackId == 1).FinishDate);
    }

    [Fact]
    public void ParallelDetourSplitsTheDay_ThenReturnsToEqualShares()
    {
        var main = Track(1, "dsa", ("a1", 60), ("a2", 60), ("a3", 60), ("a4", 60));
        var detour = Track(2, "rag", ("r1", 60), ("r2", 60));
        var phases = new List<SharePhase>
        {
            new(Monday, new Dictionary<int, int> { [1] = 60, [2] = 40 }),
            new(Monday.AddDays(3), new Dictionary<int, int> { [1] = 100 })
        };
        var endDates = new Dictionary<int, DateOnly> { [2] = Monday.AddDays(2) };

        var result = Scheduler.Build(Request(Monday, 100, new[] { main, detour }, phases: phases, endDates: endDates));

        var detourEntries = result.Entries.Where(e => e.TrackId == 2).ToList();
        Assert.Equal(2, detourEntries.Count);
        Assert.All(detourEntries, e => Assert.Equal(40, e.Minutes));
        var laterMain = result.Entries.Where(e => e.TrackId == 1 && e.Date >= Monday.AddDays(3)).ToList();
        Assert.NotEmpty(laterMain);
        Assert.All(laterMain, e => Assert.Equal(60, e.Minutes));
    }

    [Fact]
    public void AStartedTrackCanWaitUntilTomorrow()
    {
        var track = Track(1, "csharp", ("l1", 30), ("l2", 30));
        var startDates = new Dictionary<int, DateOnly> { [1] = Monday.AddDays(1) };

        var result = Scheduler.Build(Request(Monday, 60, new[] { track }, startDates: startDates));

        Assert.Equal(Monday.AddDays(1), result.Entries.First().Date);
        Assert.Equal(Monday.AddDays(2), result.Projections.Single().FinishDate);
    }

    [Fact]
    public void StartingLaterShiftsTheWholePlanForward()
    {
        var track = Track(1, "csharp", ("l1", 30), ("l2", 30));
        var onTime = Scheduler.Build(Request(Monday, 60, new[] { track }));
        var late = Scheduler.Build(Request(Monday.AddDays(2), 60, new[] { track }));

        Assert.Equal(Monday, onTime.Entries.First().Date);
        Assert.Equal(Monday.AddDays(2), late.Entries.First().Date);
        Assert.True(late.Projections.Single().FinishDate > onTime.Projections.Single().FinishDate);
    }

    [Fact]
    public void NoLessonsMeansNoEntries()
    {
        var result = Scheduler.Build(Request(Monday, 60, new[] { Track(1, "empty") }));

        Assert.Empty(result.Entries);
        Assert.Empty(result.Projections);
    }

    [Fact]
    public void ShareResolutionRoundsAndNormalises()
    {
        var tracks = new[]
        {
            Track(1, "a", ("a1", 10)),
            Track(2, "b", ("b1", 10)),
            Track(3, "c", ("c1", 10))
        };
        var request = Request(Monday, 90, tracks, shares: new Dictionary<int, int> { [1] = 1, [2] = 1, [3] = 1 });

        var shares = Scheduler.ResolveShares(request, tracks, Monday);

        Assert.Equal(100, shares.Values.Sum());
        Assert.Equal(34, shares[1]);
        Assert.Equal(33, shares[2]);
        Assert.Equal(33, shares[3]);
    }
}
