namespace LearningHub.Core.Models;

public class Skill
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Area { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsCustom { get; set; }
    public bool IsDsa { get; set; }
    public int SortOrder { get; set; }
    public string? DsaTopicsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Lesson
{
    public string Id { get; set; } = "";
    public string SkillId { get; set; } = "";
    public string? Topic { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public int EstimatedMinutes { get; set; } = 30;
    public string ContentJson { get; set; } = "{}";
    public string FilePath { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Track
{
    public int Id { get; set; }
    public string SkillId { get; set; } = "";
    public TrackState State { get; set; } = TrackState.Active;
    public int? TimeSplitPercent { get; set; }
    public string? PausedReason { get; set; }
    public bool DsaOverride { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class TimeBox
{
    public int Id { get; set; }
    public string SkillId { get; set; } = "";
    public int TrackId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeBoxMode Mode { get; set; }
    public int DetourPercent { get; set; }
    public TimeBoxStatus Status { get; set; }
    public string PausedTrackIdsJson { get; set; } = "[]";
    public string Label { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PlanSettings
{
    public int Id { get; set; } = 1;
    public int DailyMinutes { get; set; } = 90;
    public string RestWeekdaysCsv { get; set; } = "Saturday,Sunday";
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int BugFixFrequency { get; set; } = 25;
    public string? AiProviderOverride { get; set; }
    public string Theme { get; set; } = "system";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PlanEntry
{
    public int Id { get; set; }
    public int TrackId { get; set; }
    public string LessonId { get; set; } = "";
    public DateOnly Date { get; set; }
    public int Minutes { get; set; }
    public PlanEntryStatus Status { get; set; }
    public int Sequence { get; set; }
    public string? GoogleEventId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class RestDay
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
}

public class LessonProgress
{
    public int Id { get; set; }
    public string LessonId { get; set; } = "";
    public int TrackId { get; set; }
    public LessonStatus Status { get; set; }
    public int CurrentStep { get; set; }
    public string? LearnedNote { get; set; }
    public int? ConfidenceRating { get; set; }
    public string? ExplainBack { get; set; }
    public string? ExplainGrade { get; set; }
    public bool TaskDone { get; set; }
    public bool BugFixDone { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}

public class Question
{
    public int Id { get; set; }
    public string? SourceKey { get; set; }
    public string? LessonId { get; set; }
    public string SkillId { get; set; } = "";
    public QuestionKind Kind { get; set; }
    public string Prompt { get; set; } = "";
    public string OptionsJson { get; set; } = "[]";
    public int CorrectIndex { get; set; }
    public string Explanation { get; set; } = "";
    public string OptionExplanationsJson { get; set; } = "[]";
    public string? Code { get; set; }
    public string? Answer { get; set; }
    public bool IsCustom { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AnswerRecord
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public int? TrackId { get; set; }
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
    public bool Correct { get; set; }
    public int? SelectedIndex { get; set; }
    public DateOnly NextReviewDate { get; set; }
    public double Ease { get; set; } = 2.5;
    public int IntervalDays { get; set; }
    public int Repetitions { get; set; }
}

public class Mistake
{
    public int Id { get; set; }
    public string SkillId { get; set; } = "";
    public string? LessonId { get; set; }
    public int? QuestionId { get; set; }
    public int? TrackId { get; set; }
    public MistakeKind Kind { get; set; }
    public string ReasonTag { get; set; } = "";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Note
{
    public int Id { get; set; }
    public string? SkillId { get; set; }
    public string? LessonId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ConfidenceHistory
{
    public int Id { get; set; }
    public string SkillId { get; set; } = "";
    public DateOnly Date { get; set; }
    public double Score { get; set; }
    public double? SelfRating { get; set; }
    public double? Accuracy { get; set; }
    public double? TaskScore { get; set; }
    public int DaysSincePractice { get; set; }
}

public class Resource
{
    public int Id { get; set; }
    public string? SkillId { get; set; }
    public string? LessonId { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public ResourceType Type { get; set; }
    public string? Notes { get; set; }
    public ResourceStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Reflection
{
    public int Id { get; set; }
    public DateOnly WeekStart { get; set; }
    public string WentWell { get; set; } = "";
    public string WasHard { get; set; } = "";
    public string ToChange { get; set; } = "";
    public string SummaryJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ReflectionSuggestion
{
    public int Id { get; set; }
    public int ReflectionId { get; set; }
    public string Text { get; set; } = "";
    public string? Rationale { get; set; }
    public SuggestionState State { get; set; }
}

public class CompletedDsaTopic
{
    public int Id { get; set; }
    public int TrackId { get; set; }
    public string Topic { get; set; } = "";
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

public class GoogleToken
{
    public int Id { get; set; } = 1;
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string? Email { get; set; }
}
