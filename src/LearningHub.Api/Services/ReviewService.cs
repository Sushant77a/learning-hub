using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Core.Models;
using LearningHub.Core.Srs;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class QuestionDto
{
    public int Id { get; set; }
    public string? LessonId { get; set; }
    public string LessonTitle { get; set; } = "";
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public string? Topic { get; set; }
    public string Kind { get; set; } = "check";
    public string Prompt { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public string? Code { get; set; }
    public bool FromPausedTrack { get; set; }
}

public sealed class AnswerResultDto
{
    public bool Correct { get; set; }
    public int CorrectIndex { get; set; }
    public string Explanation { get; set; } = "";
    public List<string> OptionExplanations { get; set; } = new();
    public string? Answer { get; set; }
    public int? MistakeId { get; set; }
    public string NextReviewDate { get; set; } = "";
    public int IntervalDays { get; set; }
    public double Ease { get; set; }
}

public sealed class ReviewService(HubDbContext db)
{
    public async Task<List<QuestionDto>> GetDueAsync(string? skillId, int limit, DateOnly today, CancellationToken ct = default)
    {
        var questions = await db.Questions.AsNoTracking().ToListAsync(ct);
        var lessons = await db.Lessons.AsNoTracking().ToDictionaryAsync(l => l.Id, ct);
        var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var latest = await LatestAnswersAsync(ct);
        var completedLessonIds = await db.LessonProgress
            .Where(p => p.Status == LessonStatus.Completed && p.LessonId != null)
            .Select(p => p.LessonId!)
            .Distinct()
            .ToListAsync(ct);
        var completed = completedLessonIds.ToHashSet();
        var pausedSkills = await db.Tracks
            .Where(t => t.State == TrackState.Paused)
            .Select(t => t.SkillId)
            .Distinct()
            .ToListAsync(ct);

        var due = new List<(Question Question, bool IsDue)>();
        foreach (var question in questions)
        {
            if (skillId is not null && question.SkillId != skillId) continue;
            if (latest.TryGetValue(question.Id, out var answer))
            {
                if (answer.NextReviewDate <= today)
                    due.Add((question, true));
            }
            else if (question.LessonId is not null && completed.Contains(question.LessonId))
            {
                due.Add((question, false));
            }
        }

        return due
            .OrderByDescending(d => d.IsDue)
            .ThenBy(d => d.Question.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(d => Map(d.Question, lessons, skills, pausedSkills.Contains(d.Question.SkillId)))
            .ToList();
    }

    public async Task<int> DueCountAsync(DateOnly today, CancellationToken ct = default)
    {
        var questions = await db.Questions.AsNoTracking().ToListAsync(ct);
        var latest = await LatestAnswersAsync(ct);
        var completedLessonIds = await db.LessonProgress
            .Where(p => p.Status == LessonStatus.Completed)
            .Select(p => p.LessonId)
            .Distinct()
            .ToListAsync(ct);
        var completed = completedLessonIds.ToHashSet();

        var count = 0;
        foreach (var question in questions)
        {
            if (latest.TryGetValue(question.Id, out var answer))
            {
                if (answer.NextReviewDate <= today) count++;
            }
            else if (question.LessonId is not null && completed.Contains(question.LessonId))
            {
                count++;
            }
        }
        return count;
    }

    private async Task<Dictionary<int, AnswerRecord>> LatestAnswersAsync(CancellationToken ct)
    {
        var records = await db.AnswerRecords.AsNoTracking()
            .OrderByDescending(a => a.AnsweredAt)
            .ToListAsync(ct);
        return records.GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.First());
    }

    public async Task<List<QuestionDto>> BuildWarmupAsync(
        string lessonId,
        int trackId,
        DateOnly today,
        CancellationToken ct = default)
    {
        var current = await db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonId, ct);
        if (current is null) return new List<QuestionDto>();

        var allQuestions = await db.Questions.AsNoTracking().ToListAsync(ct);
        var lessons = await db.Lessons.AsNoTracking().ToDictionaryAsync(l => l.Id, ct);
        var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var latest = await LatestAnswersAsync(ct);
        var pausedSkills = await db.Tracks.Where(t => t.State == TrackState.Paused)
            .Select(t => t.SkillId).Distinct().ToListAsync(ct);
        var pausedSet = pausedSkills.ToHashSet();

        var completedLessonIds = await db.LessonProgress.AsNoTracking()
            .Where(p => p.TrackId == trackId && p.Status == LessonStatus.Completed)
            .Select(p => p.LessonId)
            .ToListAsync(ct);
        var completed = completedLessonIds.ToHashSet();

        var earlier = allQuestions
            .Where(q => q.LessonId is not null
                        && q.LessonId != lessonId
                        && lessons.TryGetValue(q.LessonId, out var l)
                        && l.Order < current.Order
                        && completed.Contains(q.LessonId))
            .ToList();

        var due = allQuestions
            .Where(q => latest.TryGetValue(q.Id, out var a) && a.NextReviewDate <= today)
            .Where(q => q.LessonId != lessonId)
            .ToList();

        var random = new Random(today.DayNumber * 31 + trackId * 7 + lessonId.GetHashCode());
        var picked = new List<Question>();

        void Take(IEnumerable<Question> source, int count, bool preferPaused = false)
        {
            var candidates = source
                .Where(q => picked.All(p => p.Id != q.Id))
                .OrderBy(q => random.Next())
                .ToList();
            if (preferPaused)
            {
                var pausedCandidates = candidates.Where(q => pausedSet.Contains(q.SkillId)).ToList();
                if (pausedCandidates.Count > 0)
                {
                    picked.Add(pausedCandidates[0]);
                    if (count <= 1) return;
                }
            }
            foreach (var candidate in candidates.Take(count))
            {
                if (picked.All(p => p.Id != candidate.Id))
                    picked.Add(candidate);
            }
        }

        Take(due, 1);
        Take(due.Concat(earlier), 1);
        // A low-rate recall question from a paused track.
        if (picked.Count < 3 && pausedSet.Count > 0)
            Take(due.Concat(earlier).Where(q => pausedSet.Contains(q.SkillId)), 1);
        Take(due.Concat(earlier), 3 - picked.Count);

        return picked.Take(3)
            .Select(q => Map(q, lessons, skills, pausedSet.Contains(q.SkillId)))
            .ToList();
    }

    public async Task<AnswerResultDto> AnswerAsync(
        int questionId,
        int? selectedIndex,
        bool? correct,
        int? trackId,
        string? reasonTag,
        CancellationToken ct = default)
    {
        var question = await db.Questions.FirstOrDefaultAsync(q => q.Id == questionId, ct)
                       ?? throw new KeyNotFoundException("Question not found.");

        bool isCorrect;
        if (selectedIndex is not null)
        {
            if (selectedIndex < 0) throw new ArgumentException("An answer is required.");
            isCorrect = selectedIndex == question.CorrectIndex;
        }
        else if (correct is not null)
        {
            isCorrect = correct.Value;
        }
        else
        {
            throw new ArgumentException("Provide selectedIndex or a self-assessment.");
        }

        var today = AppClock.Today;
        var previous = await db.AnswerRecords
            .Where(a => a.QuestionId == questionId)
            .OrderByDescending(a => a.AnsweredAt)
            .FirstOrDefaultAsync(ct);

        var schedule = SpacedRepetition.Grade(
            new ReviewSchedule(
                today,
                previous?.Ease ?? 2.5,
                previous?.IntervalDays ?? 0,
                previous?.Repetitions ?? 0),
            isCorrect,
            today);

        db.AnswerRecords.Add(new AnswerRecord
        {
            QuestionId = questionId,
            TrackId = trackId,
            AnsweredAt = DateTime.UtcNow,
            Correct = isCorrect,
            SelectedIndex = selectedIndex,
            NextReviewDate = schedule.NextReviewDate,
            Ease = schedule.Ease,
            IntervalDays = schedule.IntervalDays,
            Repetitions = schedule.Repetitions
        });

        int? mistakeId = null;
        if (!isCorrect)
        {
            var mistake = new Mistake
            {
                SkillId = question.SkillId,
                LessonId = question.LessonId,
                QuestionId = question.Id,
                TrackId = trackId,
                Kind = MistakeKind.WrongAnswer,
                ReasonTag = string.IsNullOrWhiteSpace(reasonTag) ? "Needs review" : reasonTag.Trim(),
                Note = null
            };
            db.Mistakes.Add(mistake);
            await db.SaveChangesAsync(ct);
            mistakeId = mistake.Id;
        }
        else
        {
            await db.SaveChangesAsync(ct);
        }

        return new AnswerResultDto
        {
            Correct = isCorrect,
            CorrectIndex = question.CorrectIndex,
            Explanation = question.Explanation,
            OptionExplanations = Deserialize(question.OptionExplanationsJson),
            Answer = question.Answer,
            MistakeId = mistakeId,
            NextReviewDate = Days.Iso(schedule.NextReviewDate),
            IntervalDays = schedule.IntervalDays,
            Ease = schedule.Ease
        };
    }

    public async Task<QuestionDto> CreateCustomAsync(
        string skillId,
        string? lessonId,
        string prompt,
        List<string> options,
        int correctIndex,
        string explanation,
        List<string>? optionExplanations,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Prompt is required.");
        if (options.Count < 2) throw new ArgumentException("At least two options are required.");
        if (correctIndex < 0 || correctIndex >= options.Count) throw new ArgumentException("Correct option is out of range.");
        if (!await db.Skills.AnyAsync(s => s.Id == skillId, ct)) throw new ArgumentException("Unknown skill.");

        var question = new Question
        {
            SourceKey = null,
            SkillId = skillId,
            LessonId = lessonId,
            Kind = QuestionKind.Custom,
            Prompt = prompt.Trim(),
            OptionsJson = JsonSerializer.Serialize(options),
            CorrectIndex = correctIndex,
            Explanation = explanation ?? "",
            OptionExplanationsJson = JsonSerializer.Serialize(optionExplanations ?? new List<string>()),
            IsCustom = true
        };
        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);

        var lessons = await db.Lessons.AsNoTracking().ToDictionaryAsync(l => l.Id, ct);
        var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        return Map(question, lessons, skills, false);
    }

    public async Task DeleteQuestionAsync(int id, CancellationToken ct = default)
    {
        var question = await db.Questions.FirstOrDefaultAsync(q => q.Id == id && q.IsCustom, ct)
                       ?? throw new KeyNotFoundException("Custom question not found.");
        var answers = await db.AnswerRecords.Where(a => a.QuestionId == id).ToListAsync(ct);
        var mistakes = await db.Mistakes.Where(m => m.QuestionId == id).ToListAsync(ct);
        db.AnswerRecords.RemoveRange(answers);
        db.Mistakes.RemoveRange(mistakes);
        db.Questions.Remove(question);
        await db.SaveChangesAsync(ct);
    }

    private static QuestionDto Map(
        Question question,
        Dictionary<string, Lesson> lessons,
        Dictionary<string, Skill> skills,
        bool fromPausedTrack)
    {
        var lesson = question.LessonId is null ? null : lessons.GetValueOrDefault(question.LessonId);
        return new QuestionDto
        {
            Id = question.Id,
            LessonId = question.LessonId,
            LessonTitle = lesson?.Title ?? "Custom question",
            SkillId = question.SkillId,
            SkillName = skills.GetValueOrDefault(question.SkillId)?.Name ?? question.SkillId,
            Topic = lesson?.Topic,
            Kind = question.Kind switch
            {
                QuestionKind.Complexity => "complexity",
                QuestionKind.Custom => "custom",
                _ => "check"
            },
            Prompt = question.Prompt,
            Options = Deserialize(question.OptionsJson),
            Code = question.Code,
            FromPausedTrack = fromPausedTrack
        };
    }

    private static List<string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { return new List<string>(); }
    }
}
