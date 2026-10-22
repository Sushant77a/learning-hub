using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class ReflectionDto
{
    public int Id { get; set; }
    public string WeekStart { get; set; } = "";
    public string WeekEnd { get; set; } = "";
    public string WentWell { get; set; } = "";
    public string WasHard { get; set; } = "";
    public string ToChange { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public List<ReflectionSuggestion> Suggestions { get; set; } = new();
    public WeeklySummaryDto Summary { get; set; } = new();
}

public sealed class ReflectionService(HubDbContext db, ProgressService progress, ILogger<ReflectionService> logger)
{
    public async Task<ReflectionDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var reflection = await db.Reflections.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return reflection is null ? null : await MapAsync(reflection, ct);
    }

    public async Task<List<ReflectionDto>> ListAsync(string? query, CancellationToken ct = default)
    {
        var reflections = await db.Reflections.AsNoTracking()
            .OrderByDescending(r => r.WeekStart)
            .ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            reflections = reflections.Where(r =>
                r.WentWell.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.WasHard.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.ToChange.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.WeekStart.ToString("yyyy-MM-dd").Contains(q)).ToList();
        }

        var result = new List<ReflectionDto>();
        foreach (var reflection in reflections)
            result.Add(await MapAsync(reflection, ct));
        return result;
    }

    public async Task<ReflectionDto> SaveAsync(
        DateOnly weekStart,
        string wentWell,
        string wasHard,
        string toChange,
        CancellationToken ct = default)
    {
        weekStart = Days.StartOfWeek(weekStart);
        var summary = await progress.GetWeekSummaryAsync(weekStart, ct);
        var summaryJson = JsonSerializer.Serialize(summary);
        var reflection = await db.Reflections.FirstOrDefaultAsync(r => r.WeekStart == weekStart, ct);
        if (reflection is null)
        {
            reflection = new Reflection { WeekStart = weekStart };
            db.Reflections.Add(reflection);
        }
        reflection.WentWell = wentWell ?? "";
        reflection.WasHard = wasHard ?? "";
        reflection.ToChange = toChange ?? "";
        reflection.SummaryJson = summaryJson;
        reflection.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await MapAsync(reflection, ct);
    }

    public async Task<ReflectionDto> GetCurrentAsync(DateOnly weekStart, CancellationToken ct = default)
    {
        weekStart = Days.StartOfWeek(weekStart);
        var existing = await db.Reflections.AsNoTracking().FirstOrDefaultAsync(r => r.WeekStart == weekStart, ct);
        if (existing is not null) return await MapAsync(existing, ct);
        return new ReflectionDto
        {
            WeekStart = Days.Iso(weekStart),
            WeekEnd = Days.Iso(weekStart.AddDays(6)),
            Summary = await progress.GetWeekSummaryAsync(weekStart, ct)
        };
    }

    public async Task SetSuggestionStateAsync(int id, string state, CancellationToken ct = default)
    {
        var suggestion = await db.ReflectionSuggestions.FirstOrDefaultAsync(s => s.Id == id, ct)
                         ?? throw new KeyNotFoundException("Suggestion not found.");
        suggestion.State = state switch
        {
            "accepted" => SuggestionState.Accepted,
            "declined" => SuggestionState.Declined,
            _ => SuggestionState.Pending
        };
        await db.SaveChangesAsync(ct);
    }

    private async Task<ReflectionDto> MapAsync(Reflection reflection, CancellationToken ct)
    {
        WeeklySummaryDto summary;
        try
        {
            summary = JsonSerializer.Deserialize<WeeklySummaryDto>(reflection.SummaryJson)
                      ?? await progress.GetWeekSummaryAsync(reflection.WeekStart, ct);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Reflection summary could not be parsed");
            summary = await progress.GetWeekSummaryAsync(reflection.WeekStart, ct);
        }

        var suggestions = await db.ReflectionSuggestions.AsNoTracking()
            .Where(s => s.ReflectionId == reflection.Id)
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

        return new ReflectionDto
        {
            Id = reflection.Id,
            WeekStart = Days.Iso(reflection.WeekStart),
            WeekEnd = Days.Iso(reflection.WeekStart.AddDays(6)),
            WentWell = reflection.WentWell,
            WasHard = reflection.WasHard,
            ToChange = reflection.ToChange,
            CreatedAt = reflection.UpdatedAt.ToString("O"),
            Suggestions = suggestions,
            Summary = summary
        };
    }
}
