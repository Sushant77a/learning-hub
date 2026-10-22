using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Core.Confidence;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class SkillCardDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Area { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsCustom { get; set; }
    public bool IsDsa { get; set; }
    public int? TrackId { get; set; }
    public string TrackState { get; set; } = "notstarted";
    public int LessonsDone { get; set; }
    public int LessonsTotal { get; set; }
    public string? LastPractised { get; set; }
    public int DaysSincePractice { get; set; }
    public double Confidence { get; set; }
    public double? SelfRating { get; set; }
    public double? Accuracy { get; set; }
    public double? TaskScore { get; set; }
    public bool NeedsRefresh { get; set; }
    public List<WeakTopicDto> WeakSubtopics { get; set; } = new();
    public string? CurrentDsaTopic { get; set; }
}

public sealed class WeakTopicDto
{
    public string Name { get; set; } = "";
    public double Accuracy { get; set; }
    public int Attempts { get; set; }
}

public sealed class SkillLessonDto
{
    public string Id { get; set; } = "";
    public int Order { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Topic { get; set; }
    public int EstimatedMinutes { get; set; }
    public string Status { get; set; } = "notstarted";
    public int? Confidence { get; set; }
    public string? CompletedAt { get; set; }
    public bool Locked { get; set; }
}

public sealed class DsaTopicDto
{
    public string Name { get; set; } = "";
    public bool Completed { get; set; }
    public int Lessons { get; set; }
    public int LessonsDone { get; set; }
}

public sealed class SkillDetailDto
{
    public SkillCardDto Card { get; set; } = new();
    public List<SkillLessonDto> Lessons { get; set; } = new();
    public List<Resource> Resources { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
    public List<ConfidencePointDto> ConfidenceHistory { get; set; } = new();
    public List<DsaTopicDto> DsaTopics { get; set; } = new();
}

public sealed class ConfidencePointDto
{
    public string Date { get; set; } = "";
    public double Score { get; set; }
}

public sealed class SkillService(HubDbContext db)
{
    public async Task<List<SkillCardDto>> GetAllAsync(bool recordHistory, CancellationToken ct = default)
    {
        var today = AppClock.Today;
        var data = await LoadDataAsync(ct);
        var cards = new List<SkillCardDto>();

        foreach (var skill in data.Skills.Values.OrderBy(s => s.SortOrder).ThenBy(s => s.Area).ThenBy(s => s.Name))
        {
            var card = ComputeCard(skill, data, today);
            cards.Add(card);

            if (recordHistory && card.Confidence > 0 && card.TrackState != "notstarted")
                RecordHistory(skill.Id, card, today);
        }

        await db.SaveChangesAsync(ct);
        return cards.OrderBy(c => c.Area).ThenBy(c => c.Name).ToList();
    }

    public async Task<SkillDetailDto?> GetDetailAsync(string skillId, CancellationToken ct = default)
    {
        var today = AppClock.Today;
        var data = await LoadDataAsync(ct);
        if (!data.Skills.TryGetValue(skillId, out var skill)) return null;

        var card = ComputeCard(skill, data, today);
        if (card.Confidence > 0 && card.TrackState != "notstarted")
        {
            RecordHistory(skillId, card, today);
            await db.SaveChangesAsync(ct);
        }

        var track = data.PickTrack(skillId);
        var progress = track is null
            ? new List<LessonProgress>()
            : data.Progress.GetValueOrDefault(track.Id, new List<LessonProgress>());
        var progressByLesson = progress.ToDictionary(p => p.LessonId);
        var completed = progress.Where(p => p.Status == LessonStatus.Completed).Select(p => p.LessonId).ToHashSet();
        var dsaTopics = ParseTopics(skill);
        var completedTopics = track is null
            ? new HashSet<string>()
            : data.CompletedTopics.GetValueOrDefault(track.Id, new HashSet<string>());
        var currentTopic = skill.IsDsa
            ? ScheduleService.SelectRemainingLessons(
                    skill,
                    data.LessonsBySkill.GetValueOrDefault(skillId, new List<Lesson>()),
                    completed,
                    completedTopics)
                .FirstOrDefault()?.Topic
            : null;

        var lessons = data.LessonsBySkill.GetValueOrDefault(skillId, new List<Lesson>())
            .OrderBy(l => l.Order)
            .Select(l =>
            {
                progressByLesson.TryGetValue(l.Id, out var p);
                var topicIndex = dsaTopics.FindIndex(t => string.Equals(t, l.Topic, StringComparison.OrdinalIgnoreCase));
                var currentIndex = dsaTopics.FindIndex(t => string.Equals(t, currentTopic, StringComparison.OrdinalIgnoreCase));
                var locked = skill.IsDsa
                             && currentIndex >= 0
                             && topicIndex > currentIndex
                             && !completedTopics.Contains(l.Topic ?? "");
                return new SkillLessonDto
                {
                    Id = l.Id,
                    Order = l.Order,
                    Title = l.Title,
                    Summary = l.Summary,
                    Topic = l.Topic,
                    EstimatedMinutes = l.EstimatedMinutes,
                    Status = p?.Status.ToString().ToLowerInvariant() ?? "notstarted",
                    Confidence = p?.ConfidenceRating,
                    CompletedAt = p?.CompletedAt is { } completedAt ? Days.Iso(DateOnly.FromDateTime(completedAt)) : null,
                    Locked = locked
                };
            }).ToList();

        return new SkillDetailDto
        {
            Card = card,
            Lessons = lessons,
            Resources = await db.Resources.AsNoTracking()
                .Where(r => r.SkillId == skillId)
                .OrderByDescending(r => r.UpdatedAt)
                .ToListAsync(ct),
            Notes = await db.Notes.AsNoTracking()
                .Where(n => n.SkillId == skillId)
                .OrderByDescending(n => n.UpdatedAt)
                .ToListAsync(ct),
            ConfidenceHistory = await db.ConfidenceHistory.AsNoTracking()
                .Where(c => c.SkillId == skillId)
                .OrderByDescending(c => c.Date)
                .Take(40)
                .ToListAsync(ct)
                .ContinueWith(t => t.Result
                    .OrderBy(c => c.Date)
                    .Select(c => new ConfidencePointDto { Date = Days.Iso(c.Date), Score = Math.Round(c.Score, 2) })
                    .ToList(), ct),
            DsaTopics = dsaTopics.Select(topic =>
            {
                var topicLessons = data.LessonsBySkill.GetValueOrDefault(skillId, new List<Lesson>())
                    .Where(l => string.Equals(l.Topic, topic, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return new DsaTopicDto
                {
                    Name = topic,
                    Lessons = topicLessons.Count,
                    LessonsDone = topicLessons.Count(l => completed.Contains(l.Id)),
                    Completed = (topicLessons.Count > 0 && topicLessons.All(l => completed.Contains(l.Id)))
                                || completedTopics.Contains(topic)
                };
            }).ToList()
        };
    }

    private SkillCardDto ComputeCard(Skill skill, SkillData data, DateOnly today)
    {
        var todayDt = DateTime.UtcNow;
        var track = data.PickTrack(skill.Id);
        var lessons = data.LessonsBySkill.GetValueOrDefault(skill.Id, new List<Lesson>());
        var progress = track is null
            ? new List<LessonProgress>()
            : data.Progress.GetValueOrDefault(track.Id, new List<LessonProgress>());
        var completed = progress.Where(p => p.Status == LessonStatus.Completed).ToList();
        var questionIds = data.QuestionIdsBySkill.GetValueOrDefault(skill.Id, new HashSet<int>());
        var answers = data.Answers.Where(a => questionIds.Contains(a.QuestionId)).ToList();
        var recentAnswers = answers.OrderByDescending(a => a.AnsweredAt).Take(60).ToList();
        var selfRatings = completed
            .Where(p => p.ConfidenceRating is not null)
            .OrderByDescending(p => p.CompletedAt ?? p.LastActivityAt)
            .Take(5)
            .Select(p => (double)p.ConfidenceRating!.Value)
            .ToList();

        var taskDone = completed.Count(p => p.TaskDone);
        var failedTasks = data.Mistakes.Count(m => m.SkillId == skill.Id && m.Kind == MistakeKind.FailedTask);
        var lastDates = new List<DateTime>();
        if (completed.Count > 0) lastDates.Add(completed.Max(p => p.CompletedAt ?? p.LastActivityAt));
        if (recentAnswers.Count > 0) lastDates.Add(recentAnswers.Max(a => a.AnsweredAt));
        var mistakes = data.Mistakes.Where(m => m.SkillId == skill.Id).ToList();
        if (mistakes.Count > 0) lastDates.Add(mistakes.Max(m => m.CreatedAt));

        var lastPractice = lastDates.Count > 0 ? lastDates.Max() : (DateTime?)null;
        var daysSince = lastPractice is null
            ? 0
            : Math.Max(0, (today.DayNumber - DateOnly.FromDateTime(lastPractice.Value).DayNumber));

        var confidence = ConfidenceCalculator.Calculate(new ConfidenceInputs(
            selfRatings.Count > 0 ? selfRatings.Average() : null,
            recentAnswers.Count(a => a.Correct),
            recentAnswers.Count,
            taskDone,
            taskDone + failedTasks,
            daysSince));

        var weak = BuildWeakTopics(skill, data, answers);

        var state = track?.State switch
        {
            TrackState.Active => "active",
            TrackState.Paused => "paused",
            TrackState.Completed => "completed",
            _ => "notstarted"
        };

        if (lessons.Count > 0 && completed.Count >= lessons.Count && track is not null)
            state = "completed";

        return new SkillCardDto
        {
            Id = skill.Id,
            Name = skill.Name,
            Area = skill.Area,
            Description = skill.Description,
            IsCustom = skill.IsCustom,
            IsDsa = skill.IsDsa,
            TrackId = track?.Id,
            TrackState = state,
            LessonsDone = completed.Count,
            LessonsTotal = lessons.Count,
            LastPractised = lastPractice is null ? null : Days.Iso(DateOnly.FromDateTime(lastPractice.Value)),
            DaysSincePractice = daysSince,
            Confidence = confidence.Score,
            SelfRating = confidence.SelfRating,
            Accuracy = confidence.Accuracy,
            TaskScore = confidence.TaskScore,
            NeedsRefresh = confidence.NeedsRefresh,
            WeakSubtopics = weak,
            CurrentDsaTopic = skill.IsDsa && track is not null
                ? ScheduleService.SelectRemainingLessons(
                        skill,
                        lessons,
                        completed.Select(p => p.LessonId).ToHashSet(),
                        data.CompletedTopics.GetValueOrDefault(track.Id, new HashSet<string>()))
                    .FirstOrDefault()?.Topic
                : null
        };
    }

    private static List<WeakTopicDto> BuildWeakTopics(Skill skill, SkillData data, List<AnswerRecord> answers)
    {
        var questionById = data.Questions.ToDictionary(q => q.Id);
        return answers
            .Where(a => questionById.ContainsKey(a.QuestionId))
            .GroupBy(a =>
            {
                var q = questionById[a.QuestionId];
                var lesson = q.LessonId is not null ? data.Lessons.GetValueOrDefault(q.LessonId) : null;
                return lesson?.Topic ?? lesson?.Title ?? skill.Name;
            })
            .Select(g => new WeakTopicDto
            {
                Name = g.Key,
                Attempts = g.Count(),
                Accuracy = g.Count() == 0 ? 0 : Math.Round((double)g.Count(a => a.Correct) / g.Count(), 2)
            })
            .Where(w => w.Attempts >= 1 && w.Accuracy < 0.75)
            .OrderBy(w => w.Accuracy)
            .ThenByDescending(w => w.Attempts)
            .Take(3)
            .ToList();
    }

    private void RecordHistory(string skillId, SkillCardDto card, DateOnly today)
    {
        var existing = db.ConfidenceHistory.Local
            .FirstOrDefault(c => c.SkillId == skillId && c.Date == today);
        if (existing is null)
        {
            existing = db.ConfidenceHistory
                .Where(c => c.SkillId == skillId && c.Date == today)
                .ToList()
                .FirstOrDefault();
        }

        if (existing is null)
        {
            existing = new ConfidenceHistory { SkillId = skillId, Date = today };
            db.ConfidenceHistory.Add(existing);
        }

        existing.Score = card.Confidence;
        existing.SelfRating = card.SelfRating;
        existing.Accuracy = card.Accuracy;
        existing.TaskScore = card.TaskScore;
        existing.DaysSincePractice = card.DaysSincePractice;
    }

    private async Task<SkillData> LoadDataAsync(CancellationToken ct)
    {
        var data = new SkillData
        {
            Skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct),
            Lessons = await db.Lessons.AsNoTracking().ToDictionaryAsync(l => l.Id, ct),
            Questions = await db.Questions.AsNoTracking().ToListAsync(ct),
            Tracks = await db.Tracks.AsNoTracking().ToListAsync(ct),
            Progress = await db.LessonProgress.AsNoTracking().GroupBy(p => p.TrackId)
                .ToDictionaryAsync(g => g.Key, g => g.ToList(), ct),
            Answers = await db.AnswerRecords.AsNoTracking().ToListAsync(ct),
            Mistakes = await db.Mistakes.AsNoTracking().ToListAsync(ct),
            CompletedTopics = await db.CompletedDsaTopics.AsNoTracking().GroupBy(t => t.TrackId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(t => t.Topic).ToHashSet(), ct)
        };
        data.LessonsBySkill = data.Lessons.Values.GroupBy(l => l.SkillId)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Order).ToList());
        data.QuestionIdsBySkill = data.Questions.Where(q => q.IsCustom || q.LessonId != null)
            .GroupBy(q => q.SkillId)
            .ToDictionary(g => g.Key, g => g.Select(q => q.Id).ToHashSet());
        return data;
    }

    public static List<string> ParseTopics(Skill skill)
    {
        if (string.IsNullOrWhiteSpace(skill.DsaTopicsJson)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(skill.DsaTopicsJson) ?? new List<string>(); }
        catch (JsonException) { return new List<string>(); }
    }

    private sealed class SkillData
    {
        public Dictionary<string, Skill> Skills { get; set; } = new();
        public Dictionary<string, Lesson> Lessons { get; set; } = new();
        public Dictionary<string, List<Lesson>> LessonsBySkill { get; set; } = new();
        public List<Question> Questions { get; set; } = new();
        public Dictionary<string, HashSet<int>> QuestionIdsBySkill { get; set; } = new();
        public List<Track> Tracks { get; set; } = new();
        public Dictionary<int, List<LessonProgress>> Progress { get; set; } = new();
        public List<AnswerRecord> Answers { get; set; } = new();
        public List<Mistake> Mistakes { get; set; } = new();
        public Dictionary<int, HashSet<string>> CompletedTopics { get; set; } = new();

        public Track? PickTrack(string skillId)
            => Tracks.Where(t => t.SkillId == skillId)
                .OrderBy(t => t.State == TrackState.Active ? 0 : t.State == TrackState.Paused ? 1 : 2)
                .ThenByDescending(t => t.Id)
                .FirstOrDefault();
    }
}
