using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class BackupDocument
{
    public int Version { get; set; } = 1;
    public string ExportedAt { get; set; } = DateTime.UtcNow.ToString("O");
    public PlanSettings? Settings { get; set; }
    public List<Skill> Skills { get; set; } = new();
    public List<Lesson> Lessons { get; set; } = new();
    public List<Track> Tracks { get; set; } = new();
    public List<TimeBox> TimeBoxes { get; set; } = new();
    public List<PlanEntry> PlanEntries { get; set; } = new();
    public List<RestDay> RestDays { get; set; } = new();
    public List<LessonProgress> LessonProgress { get; set; } = new();
    public List<Question> Questions { get; set; } = new();
    public List<AnswerRecord> AnswerRecords { get; set; } = new();
    public List<Mistake> Mistakes { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
    public List<ConfidenceHistory> ConfidenceHistory { get; set; } = new();
    public List<Resource> Resources { get; set; } = new();
    public List<Reflection> Reflections { get; set; } = new();
    public List<ReflectionSuggestion> ReflectionSuggestions { get; set; } = new();
    public List<CompletedDsaTopic> CompletedDsaTopics { get; set; } = new();
}

public sealed class BackupService(HubDbContext db)
{
    public async Task<BackupDocument> ExportAsync(CancellationToken ct = default)
    {
        return new BackupDocument
        {
            ExportedAt = DateTime.UtcNow.ToString("O"),
            Settings = await db.PlanSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct),
            Skills = await db.Skills.AsNoTracking().ToListAsync(ct),
            Lessons = await db.Lessons.AsNoTracking().ToListAsync(ct),
            Tracks = await db.Tracks.AsNoTracking().ToListAsync(ct),
            TimeBoxes = await db.TimeBoxes.AsNoTracking().ToListAsync(ct),
            PlanEntries = await db.PlanEntries.AsNoTracking().ToListAsync(ct),
            RestDays = await db.RestDays.AsNoTracking().ToListAsync(ct),
            LessonProgress = await db.LessonProgress.AsNoTracking().ToListAsync(ct),
            Questions = await db.Questions.AsNoTracking().ToListAsync(ct),
            AnswerRecords = await db.AnswerRecords.AsNoTracking().ToListAsync(ct),
            Mistakes = await db.Mistakes.AsNoTracking().ToListAsync(ct),
            Notes = await db.Notes.AsNoTracking().ToListAsync(ct),
            ConfidenceHistory = await db.ConfidenceHistory.AsNoTracking().ToListAsync(ct),
            Resources = await db.Resources.AsNoTracking().ToListAsync(ct),
            Reflections = await db.Reflections.AsNoTracking().ToListAsync(ct),
            ReflectionSuggestions = await db.ReflectionSuggestions.AsNoTracking().ToListAsync(ct),
            CompletedDsaTopics = await db.CompletedDsaTopics.AsNoTracking().ToListAsync(ct)
        };
    }

    public async Task<BackupImportResult> ImportAsync(BackupDocument document, CancellationToken ct = default)
    {
        if (document.Version <= 0)
            throw new ArgumentException("This is not a Learning Hub backup.");
        if (document.Skills.Count == 0)
            throw new ArgumentException("The backup contains no skills.");
        if (document.Settings is null)
            throw new ArgumentException("The backup is missing settings.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.AnswerRecords.ExecuteDeleteAsync(ct);
        await db.Mistakes.ExecuteDeleteAsync(ct);
        await db.ConfidenceHistory.ExecuteDeleteAsync(ct);
        await db.ReflectionSuggestions.ExecuteDeleteAsync(ct);
        await db.Reflections.ExecuteDeleteAsync(ct);
        await db.Notes.ExecuteDeleteAsync(ct);
        await db.Resources.ExecuteDeleteAsync(ct);
        await db.LessonProgress.ExecuteDeleteAsync(ct);
        await db.Questions.ExecuteDeleteAsync(ct);
        await db.PlanEntries.ExecuteDeleteAsync(ct);
        await db.RestDays.ExecuteDeleteAsync(ct);
        await db.TimeBoxes.ExecuteDeleteAsync(ct);
        await db.CompletedDsaTopics.ExecuteDeleteAsync(ct);
        await db.Tracks.ExecuteDeleteAsync(ct);
        await db.Lessons.ExecuteDeleteAsync(ct);
        await db.Skills.ExecuteDeleteAsync(ct);
        await db.PlanSettings.ExecuteDeleteAsync(ct);
        await db.GoogleTokens.ExecuteDeleteAsync(ct);

        db.Skills.AddRange(document.Skills);
        db.Lessons.AddRange(document.Lessons);
        db.Tracks.AddRange(document.Tracks);
        db.TimeBoxes.AddRange(document.TimeBoxes);
        db.PlanEntries.AddRange(document.PlanEntries);
        db.RestDays.AddRange(document.RestDays);
        db.LessonProgress.AddRange(document.LessonProgress);
        db.Questions.AddRange(document.Questions);
        db.AnswerRecords.AddRange(document.AnswerRecords);
        db.Mistakes.AddRange(document.Mistakes);
        db.Notes.AddRange(document.Notes);
        db.ConfidenceHistory.AddRange(document.ConfidenceHistory);
        db.Resources.AddRange(document.Resources);
        db.Reflections.AddRange(document.Reflections);
        db.ReflectionSuggestions.AddRange(document.ReflectionSuggestions);
        db.CompletedDsaTopics.AddRange(document.CompletedDsaTopics);

        var settings = document.Settings;
        settings.Id = 1;
        db.PlanSettings.Add(settings);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new BackupImportResult
        {
            Skills = document.Skills.Count,
            Lessons = document.Lessons.Count,
            Tracks = document.Tracks.Count,
            PlanEntries = document.PlanEntries.Count,
            Questions = document.Questions.Count,
            Notes = document.Notes.Count,
            Message = "Backup restored. Your curriculum files remain the source of truth for lesson text."
        };
    }
}

public sealed class BackupImportResult
{
    public int Skills { get; set; }
    public int Lessons { get; set; }
    public int Tracks { get; set; }
    public int PlanEntries { get; set; }
    public int Questions { get; set; }
    public int Notes { get; set; }
    public string Message { get; set; } = "";
}
