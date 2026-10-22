using System.Text.Json;
using System.Text.Json.Serialization;

namespace LearningHub.Core.Curriculum;

public sealed class CurriculumManifest
{
    public List<AreaDefinition> Areas { get; set; } = new();
    public List<SkillDefinition> Skills { get; set; } = new();
}

public sealed class AreaDefinition
{
    public string Name { get; set; } = "";
    public int Order { get; set; }
}

public sealed class SkillDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Area { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Directory { get; set; }
    public bool Dsa { get; set; }
    public List<string> DsaTopics { get; set; } = new();
    public string? StarterNote { get; set; }
}

public sealed class LessonFile
{
    public string Id { get; set; } = "";
    public string Skill { get; set; } = "";
    public string? Topic { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public int EstimatedMinutes { get; set; } = 30;
    public string Theory { get; set; } = "";
    public WorkedExample? WorkedExample { get; set; }
    public List<CheckQuestion> CheckQuestions { get; set; } = new();
    public string ExplainPrompt { get; set; } = "";
    public LessonTask? Task { get; set; }
    public BugFix? BugFix { get; set; }
    public DsaContent? Dsa { get; set; }
    public List<ExtraQuestion> RecallQuestions { get; set; } = new();
}

public sealed class WorkedExample
{
    public string Title { get; set; } = "";
    public string Markdown { get; set; } = "";
    public List<string> Steps { get; set; } = new();
}

public sealed class CheckQuestion
{
    public string? Id { get; set; }
    public string Prompt { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public int CorrectIndex { get; set; }
    public string Explanation { get; set; } = "";
    public List<string>? OptionExplanations { get; set; }
}

public sealed class ExtraQuestion
{
    public string? Id { get; set; }
    public string Prompt { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public int CorrectIndex { get; set; }
    public string Explanation { get; set; } = "";
    public List<string>? OptionExplanations { get; set; }
}

public sealed class LessonTask
{
    public string Title { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Language { get; set; } = "csharp";
    public string StarterCode { get; set; } = "";
    public string Solution { get; set; } = "";
    public List<string> Requirements { get; set; } = new();
}

public sealed class BugFix
{
    public string Title { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Language { get; set; } = "csharp";
    public string BrokenCode { get; set; } = "";
    public string Explanation { get; set; } = "";
}

public sealed class DsaContent
{
    public string Topic { get; set; } = "";
    public List<ComplexityQuestion> ComplexityQuestions { get; set; } = new();
    public List<PracticeProblem> Practice { get; set; } = new();
}

public sealed class ComplexityQuestion
{
    public string Prompt { get; set; } = "";
    public string Code { get; set; } = "";
    public string Language { get; set; } = "csharp";
    public string Answer { get; set; } = "";
    public string Explanation { get; set; } = "";
}

public sealed class PracticeProblem
{
    public string Title { get; set; } = "";
    public string Prompt { get; set; } = "";
    public List<string> Hints { get; set; } = new();
    public string Solution { get; set; } = "";
}

public sealed class LessonValidationResult
{
    public LessonFile? Lesson { get; init; }
    public List<string> Errors { get; init; } = new();
    public bool IsValid => Lesson is not null && Errors.Count == 0;
}

public static class CurriculumJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static LessonValidationResult ParseLesson(string json, string filePath, IReadOnlyCollection<string> knownSkills)
    {
        LessonFile? lesson;
        try
        {
            lesson = JsonSerializer.Deserialize<LessonFile>(json, Options);
        }
        catch (JsonException ex)
        {
            return new LessonValidationResult { Errors = { $"{filePath}: invalid JSON ({ex.Message})" } };
        }

        if (lesson is null)
            return new LessonValidationResult { Errors = { $"{filePath}: empty lesson" } };

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(lesson.Id)) errors.Add($"{filePath}: id is required");
        if (string.IsNullOrWhiteSpace(lesson.Skill)) errors.Add($"{filePath}: skill is required");
        else if (!knownSkills.Contains(lesson.Skill)) errors.Add($"{filePath}: unknown skill '{lesson.Skill}'");
        if (string.IsNullOrWhiteSpace(lesson.Title)) errors.Add($"{filePath}: title is required");
        if (string.IsNullOrWhiteSpace(lesson.Theory)) errors.Add($"{filePath}: theory is required");
        if (lesson.Order <= 0) errors.Add($"{filePath}: order must be a positive integer");
        if (lesson.CheckQuestions.Count == 0) errors.Add($"{filePath}: at least one check question is required");
        foreach (var (question, index) in lesson.CheckQuestions.Select((q, i) => (q, i)))
        {
            if (string.IsNullOrWhiteSpace(question.Prompt)) errors.Add($"{filePath}: checkQuestions[{index}].prompt is required");
            if (question.Options.Count < 2) errors.Add($"{filePath}: checkQuestions[{index}] needs at least two options");
            if (question.CorrectIndex < 0 || question.CorrectIndex >= question.Options.Count)
                errors.Add($"{filePath}: checkQuestions[{index}].correctIndex is out of range");
            if (string.IsNullOrWhiteSpace(question.Explanation))
                errors.Add($"{filePath}: checkQuestions[{index}].explanation is required");
        }
        if (lesson.EstimatedMinutes < 5) lesson.EstimatedMinutes = 5;

        return new LessonValidationResult { Lesson = lesson, Errors = errors };
    }
}
