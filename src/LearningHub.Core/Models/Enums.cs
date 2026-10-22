namespace LearningHub.Core.Models;

public enum TrackState
{
    NotStarted = 0,
    Active = 1,
    Paused = 2,
    Completed = 3
}

public enum LessonStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2
}

public enum PlanEntryStatus
{
    Planned = 0,
    Done = 1,
    Missed = 2
}

public enum QuestionKind
{
    Check = 0,
    Complexity = 1,
    Custom = 2
}

public enum MistakeKind
{
    WrongAnswer = 0,
    FailedTask = 1,
    BugFix = 2
}

public enum ResourceType
{
    Article = 0,
    Video = 1,
    Docs = 2,
    Other = 3
}

public enum ResourceStatus
{
    Unread = 0,
    Reading = 1,
    Read = 2,
    Watched = 3
}

public enum TimeBoxMode
{
    PauseOthers = 0,
    Parallel = 1
}

public enum TimeBoxStatus
{
    Active = 0,
    Ended = 1
}

public enum SuggestionState
{
    Pending = 0,
    Accepted = 1,
    Declined = 2
}
