using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Core.Curriculum;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class CurriculumService(IWebHostEnvironment env, ILogger<CurriculumService> logger)
{
    private readonly List<string> _errors = new();

    public IReadOnlyList<string> LastErrors => _errors;

    public string RootPath { get; } = ResolveRoot(env);

    private static string ResolveRoot(IWebHostEnvironment env)
    {
        var configured = Environment.GetEnvironmentVariable("LEARNINGHUB_CURRICULUM");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return Path.GetFullPath(configured);

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "curriculum"),
            Path.Combine(env.ContentRootPath, "curriculum"),
            Path.Combine(AppContext.BaseDirectory, "curriculum")
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
                return Path.GetFullPath(candidate);
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "curriculum");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
                return Path.GetFullPath(candidate);
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "curriculum"));
    }

    public async Task<CurriculumManifest> LoadManifestAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(RootPath, "manifest.json");
        if (!File.Exists(path))
            throw new InvalidOperationException($"Curriculum manifest not found at {path}.");
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<CurriculumManifest>(json, CurriculumJson.Options)
               ?? throw new InvalidOperationException("Curriculum manifest could not be parsed.");
    }

    public async Task<List<(LessonFile Lesson, string Path, string Json)>> ReadLessonsAsync(
        IReadOnlyCollection<string> knownSkills,
        CancellationToken ct = default)
    {
        var result = new List<(LessonFile, string, string)>();
        if (!Directory.Exists(RootPath))
            return result;

        foreach (var path in Directory.EnumerateFiles(RootPath, "*.json", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(Path.GetFileName(path), "manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var json = await File.ReadAllTextAsync(path, ct);
            var parsed = CurriculumJson.ParseLesson(json, Path.GetRelativePath(RootPath, path), knownSkills);
            if (!parsed.IsValid)
            {
                foreach (var error in parsed.Errors)
                {
                    _errors.Add(error);
                    logger.LogWarning("Curriculum: {Error}", error);
                }
                continue;
            }

            result.Add((parsed.Lesson!, path, json));
        }

        var duplicates = result.GroupBy(r => r.Item1.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        foreach (var duplicate in duplicates)
        {
            _errors.Add($"Duplicate lesson id '{duplicate}'. Only the first one is used.");
            logger.LogWarning("Curriculum: duplicate lesson id {Id}", duplicate);
        }

        return result.GroupBy(r => r.Item1.Id).Select(g => g.First()).ToList();
    }

    /// <summary>Upserts manifest skills and all lesson files, including their questions.</summary>
    public async Task<CurriculumSyncResult> SyncAsync(HubDbContext db, CancellationToken ct = default)
    {
        _errors.Clear();
        var manifest = await LoadManifestAsync(ct);

        for (var i = 0; i < manifest.Skills.Count; i++)
        {
            var definition = manifest.Skills[i];
            var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == definition.Id, ct);
            if (skill is null)
            {
                skill = new Skill { Id = definition.Id, IsCustom = false };
                db.Skills.Add(skill);
            }

            skill.Name = definition.Name;
            skill.Area = definition.Area;
            skill.Description = definition.Description;
            skill.IsDsa = definition.Dsa;
            skill.SortOrder = i;
            if (definition.DsaTopics.Count > 0)
                skill.DsaTopicsJson = JsonSerializer.Serialize(definition.DsaTopics);
        }

        await db.SaveChangesAsync(ct);

        var skillIds = manifest.Skills.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lessons = await ReadLessonsAsync(skillIds, ct);
        var existingQuestions = await db.Questions.Where(q => q.SourceKey != null).ToListAsync(ct);
        var questionByKey = existingQuestions
            .Where(q => q.SourceKey is not null)
            .GroupBy(q => q.SourceKey!)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var (lessonFile, path, json) in lessons)
        {
            var lesson = await db.Lessons.FirstOrDefaultAsync(l => l.Id == lessonFile.Id, ct);
            if (lesson is null)
            {
                lesson = new Lesson { Id = lessonFile.Id };
                db.Lessons.Add(lesson);
            }

            lesson.SkillId = lessonFile.Skill;
            lesson.Topic = lessonFile.Topic ?? lessonFile.Dsa?.Topic;
            lesson.Order = lessonFile.Order;
            lesson.Title = lessonFile.Title;
            lesson.Summary = lessonFile.Summary;
            lesson.EstimatedMinutes = lessonFile.EstimatedMinutes;
            lesson.ContentJson = json;
            lesson.FilePath = Path.GetRelativePath(RootPath, path).Replace('\\', '/');
            lesson.UpdatedAt = DateTime.UtcNow;

            var index = 0;
            foreach (var question in lessonFile.CheckQuestions)
            {
                UpsertQuestion(
                    db,
                    questionByKey,
                    $"{lessonFile.Id}#check{index}",
                    lessonFile,
                    QuestionKind.Check,
                    question.Prompt,
                    question.Options,
                    question.CorrectIndex,
                    question.Explanation,
                    question.OptionExplanations,
                    null,
                    null);
                index++;
            }

            index = 0;
            foreach (var extra in lessonFile.RecallQuestions)
            {
                UpsertQuestion(
                    db,
                    questionByKey,
                    $"{lessonFile.Id}#recall{index}",
                    lessonFile,
                    QuestionKind.Check,
                    extra.Prompt,
                    extra.Options,
                    extra.CorrectIndex,
                    extra.Explanation,
                    extra.OptionExplanations,
                    null,
                    null);
                index++;
            }

            index = 0;
            foreach (var complexity in lessonFile.Dsa?.ComplexityQuestions ?? new List<ComplexityQuestion>())
            {
                UpsertQuestion(
                    db,
                    questionByKey,
                    $"{lessonFile.Id}#complexity{index}",
                    lessonFile,
                    QuestionKind.Complexity,
                    complexity.Prompt,
                    new List<string>(),
                    0,
                    complexity.Explanation,
                    null,
                    complexity.Code,
                    complexity.Answer);
                index++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new CurriculumSyncResult(manifest.Skills.Count, lessons.Count, _errors.ToList());
    }

    private static void UpsertQuestion(
        HubDbContext db,
        Dictionary<string, Question> known,
        string key,
        LessonFile lesson,
        QuestionKind kind,
        string prompt,
        List<string> options,
        int correctIndex,
        string explanation,
        List<string>? optionExplanations,
        string? code,
        string? answer)
    {
        if (!known.TryGetValue(key, out var question))
        {
            question = new Question { SourceKey = key };
            known[key] = question;
            db.Questions.Add(question);
        }

        question.LessonId = lesson.Id;
        question.SkillId = lesson.Skill;
        question.Kind = kind;
        question.Prompt = prompt;
        question.OptionsJson = JsonSerializer.Serialize(options);
        question.CorrectIndex = correctIndex;
        question.Explanation = explanation;
        question.OptionExplanationsJson = JsonSerializer.Serialize(optionExplanations ?? new List<string>());
        question.Code = code;
        question.Answer = answer;
    }

    /// <summary>Writes an AI-generated mini-curriculum next to the hand-written lessons.</summary>
    public async Task<string> WriteGeneratedLessonAsync(LessonFile lesson, CancellationToken ct = default)
    {
        var folder = Path.Combine(RootPath, lesson.Skill);
        Directory.CreateDirectory(folder);
        var safeTopic = string.IsNullOrWhiteSpace(lesson.Topic) ? "lessons" : NameUtil.Slug(lesson.Topic);
        var topicFolder = Path.Combine(folder, safeTopic);
        Directory.CreateDirectory(topicFolder);
        var file = Path.Combine(topicFolder, $"{lesson.Order:00}-{NameUtil.Slug(lesson.Title)}.json");
        var json = JsonSerializer.Serialize(lesson, CurriculumJson.Options);
        await File.WriteAllTextAsync(file, json, ct);
        return file;
    }
}

public sealed record CurriculumSyncResult(int Skills, int Lessons, IReadOnlyList<string> Errors);
