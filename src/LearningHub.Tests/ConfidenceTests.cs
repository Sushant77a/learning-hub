using LearningHub.Core.Confidence;

namespace LearningHub.Tests;

public class ConfidenceTests
{
    [Fact]
    public void BlendsSelfRatingAccuracyAndTasks()
    {
        var result = ConfidenceCalculator.Calculate(new ConfidenceInputs(
            SelfRating: 4,
            CorrectAnswers: 8,
            TotalAnswers: 10,
            TasksPassed: 1,
            TasksAttempted: 2,
            DaysSincePractice: 0));

        Assert.True(result.HasData);
        Assert.Equal(3.7, result.Score, 2);
        Assert.Equal(0.8, result.Accuracy!.Value, 3);
        Assert.Equal(0.5, result.TaskScore!.Value, 3);
        Assert.False(result.NeedsRefresh);
    }

    [Fact]
    public void MissingComponentsAreReweighted_NotCountedAsZero()
    {
        var result = ConfidenceCalculator.Calculate(new ConfidenceInputs(
            SelfRating: 4,
            CorrectAnswers: 10,
            TotalAnswers: 10,
            TasksPassed: 0,
            TasksAttempted: 0,
            DaysSincePractice: 0));

        // self 4 (0.4) + accuracy 5 (0.4) => 4.5, reweighted over 0.8
        Assert.Equal(4.5, result.Score, 2);
        Assert.Null(result.TaskScore);
    }

    [Fact]
    public void NoDataMeansNoConfidenceScore()
    {
        var result = ConfidenceCalculator.Calculate(new ConfidenceInputs(null, 0, 0, 0, 0, 30));

        Assert.False(result.HasData);
        Assert.Equal(0, result.Score);
        Assert.False(result.NeedsRefresh);
    }

    [Fact]
    public void ConfidenceDecaysWithTimeSincePractice()
    {
        var fresh = ConfidenceCalculator.Calculate(new ConfidenceInputs(4, 8, 10, 1, 2, 0));
        var stale = ConfidenceCalculator.Calculate(new ConfidenceInputs(4, 8, 10, 1, 2, 60));

        Assert.True(stale.Score < fresh.Score);
        Assert.True(stale.DecayMultiplier < fresh.DecayMultiplier);
        Assert.True(stale.NeedsRefresh);
    }

    [Fact]
    public void DecayNeverDropsBelowTheFloor()
    {
        var result = ConfidenceCalculator.Calculate(new ConfidenceInputs(1, 0, 10, 0, 1, 3650));

        Assert.Equal(1, result.Score, 2);
        Assert.True(result.NeedsRefresh);
    }

    [Fact]
    public void AccuracyAndSelfRatingAreClamped()
    {
        var result = ConfidenceCalculator.Calculate(new ConfidenceInputs(
            SelfRating: 99,
            CorrectAnswers: 5,
            TotalAnswers: 5,
            TasksPassed: 5,
            TasksAttempted: 5,
            DaysSincePractice: 0));

        Assert.Equal(5, result.Score, 2);
        Assert.Equal(5, result.SelfRating!.Value);
    }

    [Fact]
    public void AStrongRecentSkillDoesNotNeedARefresher()
    {
        var result = ConfidenceCalculator.Calculate(new ConfidenceInputs(5, 9, 10, 2, 2, 7));

        Assert.False(result.NeedsRefresh);
        Assert.True(result.Score > 4);
    }

    [Fact]
    public void AWeakSkillNeedsARefresher()
    {
        var result = ConfidenceCalculator.Calculate(new ConfidenceInputs(2, 3, 10, 0, 2, 5));

        Assert.True(result.NeedsRefresh);
        Assert.True(result.Score < 3);
    }
}
