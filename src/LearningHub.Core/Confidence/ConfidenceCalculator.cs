namespace LearningHub.Core.Confidence;

public sealed record ConfidenceInputs(
    double? SelfRating,
    int CorrectAnswers,
    int TotalAnswers,
    int TasksPassed,
    int TasksAttempted,
    int DaysSincePractice);

public sealed record ConfidenceResult(
    double Score,
    double? SelfRating,
    double? Accuracy,
    double? TaskScore,
    double DecayMultiplier,
    bool HasData,
    bool NeedsRefresh);

/// <summary>
/// Confidence is a 0-5 blend of self-ratings (40%), quiz accuracy (40%) and task
/// results (20%), then gently decayed based on days since the last practice.
/// Missing components are re-weighted rather than counted as zero.
/// </summary>
public static class ConfidenceCalculator
{
    public const int DecayHalfLifeDays = 45;

    public static ConfidenceResult Calculate(ConfidenceInputs inputs)
    {
        var components = new List<(double Value, double Weight)>();
        var clampedSelf = inputs.SelfRating is { } rawSelf ? Math.Clamp(rawSelf, 1, 5) : (double?)null;

        if (clampedSelf is { } self)
            components.Add((self, 0.4));

        double? accuracy = inputs.TotalAnswers > 0
            ? (double)inputs.CorrectAnswers / inputs.TotalAnswers
            : null;
        if (accuracy is { } acc)
            components.Add((Math.Clamp(acc, 0, 1) * 5, 0.4));

        double? taskScore = inputs.TasksAttempted > 0
            ? (double)inputs.TasksPassed / inputs.TasksAttempted
            : null;
        if (taskScore is { } task)
            components.Add((Math.Clamp(task, 0, 1) * 5, 0.2));

        var days = Math.Max(0, inputs.DaysSincePractice);
        var decay = 0.55 + 0.45 * Math.Exp(-days / (double)DecayHalfLifeDays);

        if (components.Count == 0)
        {
            return new ConfidenceResult(0, clampedSelf, accuracy, taskScore, decay, false, false);
        }

        var totalWeight = components.Sum(c => c.Weight);
        var baseScore = components.Sum(c => c.Value * c.Weight) / totalWeight;
        var score = Math.Clamp(baseScore * decay, 1, 5);

        var needsRefresh = score < 3.0 || (days >= 14 && score < 3.8);

        return new ConfidenceResult(
            Math.Round(score, 2),
            clampedSelf,
            accuracy,
            taskScore,
            Math.Round(decay, 3),
            true,
            needsRefresh);
    }
}
