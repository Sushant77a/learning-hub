using LearningHub.Core.Models;

namespace LearningHub.Api.Services;

public sealed class PlanItemDto
{
    public int TrackId { get; set; }
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public string LessonId { get; set; } = "";
    public string LessonTitle { get; set; } = "";
    public string Date { get; set; } = "";
    public int Minutes { get; set; }
    public string Status { get; set; } = "planned";
    public bool IsDetour { get; set; }
}

public sealed class PlanTrackDto
{
    public int TrackId { get; set; }
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public string State { get; set; } = "active";
    public int SharePercent { get; set; }
    public int DailyMinutes { get; set; }
    public int LessonsDone { get; set; }
    public int LessonsTotal { get; set; }
    public string? FinishDate { get; set; }
    public string? FirstLessonDate { get; set; }
    public bool IsDetour { get; set; }
    public string? ResumeOn { get; set; }
}

public sealed class DetourDto
{
    public int TimeBoxId { get; set; }
    public int TrackId { get; set; }
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public string Label { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public int DayNumber { get; set; }
    public int TotalDays { get; set; }
    public string Mode { get; set; } = "pause";
    public int DetourPercent { get; set; }
    public string? ResumeOn { get; set; }
    public bool DecisionDue { get; set; }
    public List<string> PausedSkillNames { get; set; } = new();
}

public sealed class PlanSnapshotDto
{
    public string Today { get; set; } = "";
    public int DailyMinutes { get; set; }
    public string StartDate { get; set; } = "";
    public List<string> RestWeekdays { get; set; } = new();
    public List<string> RestDates { get; set; } = new();
    public List<PlanItemDto> Entries { get; set; } = new();
    public List<PlanTrackDto> Tracks { get; set; } = new();
    public DetourDto? Detour { get; set; }
    public List<DetourDto> TimeBoxes { get; set; } = new();
    public int MissedDaysShifted { get; set; }
    public List<string> Changes { get; set; } = new();
}

public sealed class PlanSimulation
{
    public int? DailyMinutes { get; set; }
    public string? RestWeekdaysCsv { get; set; }
    public DateOnly? StartDate { get; set; }
    public HashSet<int> PauseTrackIds { get; set; } = new();
    public HashSet<int> ResumeTrackIds { get; set; } = new();
    public List<DateOnly> AddRestDates { get; set; } = new();
    public TimeBox? SimulatedTimeBox { get; set; }
    public Track? SimulatedTrack { get; set; }
    public List<string> Changes { get; set; } = new();
}
