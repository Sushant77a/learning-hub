namespace LearningHub.Core.Srs;

public sealed record ReviewSchedule(
    DateOnly NextReviewDate,
    double Ease,
    int IntervalDays,
    int Repetitions);

/// <summary>
/// A compact SM-2 style scheduler. Wrong answers come back tomorrow and reset the
/// streak; correct answers move 1 day -> 3 days -> interval * ease.
/// </summary>
public static class SpacedRepetition
{
    public const double MinEase = 1.3;
    public const double MaxEase = 2.8;

    public static ReviewSchedule Initial(DateOnly today) => new(today, 2.5, 0, 0);

    public static ReviewSchedule Grade(ReviewSchedule current, bool correct, DateOnly today)
    {
        if (!correct)
        {
            return new ReviewSchedule(
                today.AddDays(1),
                Clamp(current.Ease - 0.2),
                1,
                0);
        }

        var repetitions = current.Repetitions + 1;
        var interval = repetitions switch
        {
            1 => 1,
            2 => 3,
            _ => Math.Max(1, (int)Math.Round(current.IntervalDays * current.Ease))
        };
        var ease = Clamp(current.Ease + 0.1);
        return new ReviewSchedule(today.AddDays(interval), ease, interval, repetitions);
    }

    public static bool IsDue(ReviewSchedule schedule, DateOnly today) => schedule.NextReviewDate <= today;

    public static ReviewSchedule FromAnswer(DateOnly today, bool correct, double? ease = null, int intervalDays = 0, int repetitions = 0)
        => Grade(new ReviewSchedule(today, ease ?? 2.5, intervalDays, repetitions), correct, today);

    private static double Clamp(double ease) => Math.Clamp(Math.Round(ease, 2), MinEase, MaxEase);
}
