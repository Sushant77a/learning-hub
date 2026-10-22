using LearningHub.Core.Srs;

namespace LearningHub.Tests;

public class SpacedRepetitionTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);

    [Fact]
    public void FirstCorrectAnswerIsDueTomorrow()
    {
        var next = SpacedRepetition.Grade(SpacedRepetition.Initial(Today), true, Today);

        Assert.Equal(Today.AddDays(1), next.NextReviewDate);
        Assert.Equal(1, next.IntervalDays);
        Assert.Equal(1, next.Repetitions);
        Assert.Equal(2.6, next.Ease, 3);
    }

    [Fact]
    public void SecondCorrectAnswerUsesThreeDayInterval()
    {
        var first = SpacedRepetition.Grade(SpacedRepetition.Initial(Today), true, Today);
        var second = SpacedRepetition.Grade(first, true, Today);

        Assert.Equal(3, second.IntervalDays);
        Assert.Equal(Today.AddDays(3), second.NextReviewDate);
        Assert.Equal(2, second.Repetitions);
        Assert.Equal(2.7, second.Ease, 3);
    }

    [Fact]
    public void LaterCorrectAnswersMultiplyByEase()
    {
        var schedule = new ReviewSchedule(Today, 2.8, 3, 2);
        var next = SpacedRepetition.Grade(schedule, true, Today);

        Assert.Equal(8, next.IntervalDays); // round(3 * 2.8)
        Assert.Equal(Today.AddDays(8), next.NextReviewDate);
        Assert.Equal(2.8, next.Ease, 3); // capped
    }

    [Fact]
    public void WrongAnswerResetsAndComesBackTomorrow()
    {
        var schedule = new ReviewSchedule(Today, 2.5, 8, 4);
        var next = SpacedRepetition.Grade(schedule, false, Today);

        Assert.Equal(1, next.IntervalDays);
        Assert.Equal(0, next.Repetitions);
        Assert.Equal(Today.AddDays(1), next.NextReviewDate);
        Assert.Equal(2.3, next.Ease, 3);
    }

    [Fact]
    public void EaseIsClampedToSensibleBounds()
    {
        var easy = SpacedRepetition.Grade(new ReviewSchedule(Today, 2.8, 10, 5), true, Today);
        Assert.Equal(SpacedRepetition.MaxEase, easy.Ease, 3);

        var hard = SpacedRepetition.Grade(new ReviewSchedule(Today, 1.3, 10, 5), false, Today);
        Assert.Equal(SpacedRepetition.MinEase, hard.Ease, 3);
    }

    [Fact]
    public void IsDueIncludesTodayAndEarlier()
    {
        var schedule = new ReviewSchedule(Today, 2.5, 1, 1);

        Assert.True(SpacedRepetition.IsDue(schedule, Today));
        Assert.True(SpacedRepetition.IsDue(schedule, Today.AddDays(1)));
        Assert.False(SpacedRepetition.IsDue(schedule, Today.AddDays(-1)));
    }

    [Fact]
    public void RepeatedFailuresNeverScheduleInThePast()
    {
        var schedule = SpacedRepetition.Initial(Today);
        for (var i = 0; i < 10; i++)
            schedule = SpacedRepetition.Grade(schedule, false, Today);

        Assert.Equal(Today.AddDays(1), schedule.NextReviewDate);
        Assert.True(schedule.Ease >= SpacedRepetition.MinEase);
    }
}
