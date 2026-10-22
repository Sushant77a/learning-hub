using LearningHub.Api.Data;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace LearningHub.Api.Services;

public sealed class SearchHitDto
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Snippet { get; set; } = "";
    public string Meta { get; set; } = "";
    public string Route { get; set; } = "";
}

public sealed class SearchGroupDto
{
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public List<SearchHitDto> Results { get; set; } = new();
}

public sealed class SearchResponse
{
    public string Query { get; set; } = "";
    public int Total { get; set; }
    public List<SearchGroupDto> Groups { get; set; } = new();
}

public sealed class SearchService(HubDbContext db)
{
    public async Task<SearchResponse> SearchAsync(string query, CancellationToken ct = default)
    {
        var q = (query ?? "").Trim();
        if (q.Length < 2)
            return new SearchResponse { Query = q };

        var pattern = $"%{EscapeLike(q)}%";
        var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var lessons = await db.Lessons.AsNoTracking()
            .Where(l => EF.Functions.Like(l.Title, pattern) || EF.Functions.Like(l.Summary, pattern) || EF.Functions.Like(l.ContentJson, pattern))
            .OrderBy(l => l.Order)
            .Take(8)
            .ToListAsync(ct);
        var notes = await db.Notes.AsNoTracking()
            .Where(n => EF.Functions.Like(n.Title, pattern) || EF.Functions.Like(n.Body, pattern))
            .OrderByDescending(n => n.UpdatedAt)
            .Take(8)
            .ToListAsync(ct);
        var questions = await db.Questions.AsNoTracking()
            .Where(x => EF.Functions.Like(x.Prompt, pattern) || EF.Functions.Like(x.Explanation, pattern))
            .Take(6)
            .ToListAsync(ct);
        var mistakes = await db.Mistakes.AsNoTracking()
            .Where(m => EF.Functions.Like(m.ReasonTag, pattern) || (m.Note != null && EF.Functions.Like(m.Note, pattern)))
            .OrderByDescending(m => m.CreatedAt)
            .Take(8)
            .ToListAsync(ct);
        var resources = await db.Resources.AsNoTracking()
            .Where(r => EF.Functions.Like(r.Title, pattern) || EF.Functions.Like(r.Url, pattern)
                        || (r.Notes != null && EF.Functions.Like(r.Notes, pattern)))
            .OrderByDescending(r => r.UpdatedAt)
            .Take(8)
            .ToListAsync(ct);
        var reflections = await db.Reflections.AsNoTracking()
            .Where(r => EF.Functions.Like(r.WentWell, pattern) || EF.Functions.Like(r.WasHard, pattern)
                        || EF.Functions.Like(r.ToChange, pattern))
            .OrderByDescending(r => r.WeekStart)
            .Take(6)
            .ToListAsync(ct);
        var skillHits = await db.Skills.AsNoTracking()
            .Where(s => EF.Functions.Like(s.Name, pattern) || EF.Functions.Like(s.Area, pattern)
                        || EF.Functions.Like(s.Description, pattern))
            .Take(6)
            .ToListAsync(ct);

        var groups = new List<SearchGroupDto>();

        void AddGroup(string type, string label, IEnumerable<SearchHitDto> hits)
        {
            var list = hits.ToList();
            if (list.Count > 0) groups.Add(new SearchGroupDto { Type = type, Label = label, Results = list });
        }

        AddGroup("lesson", "Lessons", lessons.Select(l => new SearchHitDto
        {
            Type = "lesson",
            Title = l.Title,
            Snippet = Snippet(string.IsNullOrWhiteSpace(l.Summary) ? l.ContentJson : l.Summary, q),
            Meta = $"{skills.GetValueOrDefault(l.SkillId)?.Name} · lesson {l.Order}",
            Route = $"/lesson/{l.Id}"
        }));
        AddGroup("note", "Notes", notes.Select(n => new SearchHitDto
        {
            Type = "note",
            Title = n.Title,
            Snippet = Snippet(n.Body, q),
            Meta = n.LessonId is not null ? "Lesson note" : "Skill note",
            Route = $"/notes?note={n.Id}"
        }));
        AddGroup("question", "Questions", questions.Select(x => new SearchHitDto
        {
            Type = "question",
            Title = Snippet(x.Prompt, q, 90),
            Snippet = Snippet(x.Explanation, q),
            Meta = skills.GetValueOrDefault(x.SkillId)?.Name ?? x.SkillId,
            Route = $"/review?question={x.Id}"
        }));
        AddGroup("mistake", "Mistakes", mistakes.Select(m => new SearchHitDto
        {
            Type = "mistake",
            Title = m.ReasonTag,
            Snippet = Snippet(m.Note ?? "Logged mistake", q),
            Meta = $"{skills.GetValueOrDefault(m.SkillId)?.Name} · {m.CreatedAt:yyyy-MM-dd}",
            Route = $"/progress?tab=mistakes&skill={m.SkillId}"
        }));
        AddGroup("resource", "Resources", resources.Select(r => new SearchHitDto
        {
            Type = "resource",
            Title = r.Title,
            Snippet = Snippet(r.Notes ?? r.Url, q),
            Meta = $"{r.Type} · {skills.GetValueOrDefault(r.SkillId ?? "")?.Name ?? "All skills"}",
            Route = $"/resources?resource={r.Id}"
        }));
        AddGroup("reflection", "Reflections", reflections.Select(r => new SearchHitDto
        {
            Type = "reflection",
            Title = $"Week of {r.WeekStart:d MMM yyyy}",
            Snippet = Snippet($"{r.WentWell} {r.WasHard} {r.ToChange}", q),
            Meta = "Weekly reflection",
            Route = $"/reflections?week={DayOfWeekIso(r.WeekStart)}"
        }));
        AddGroup("skill", "Skills", skillHits.Select(s => new SearchHitDto
        {
            Type = "skill",
            Title = s.Name,
            Snippet = Snippet(s.Description, q),
            Meta = s.Area,
            Route = $"/skills/{s.Id}"
        }));

        return new SearchResponse
        {
            Query = q,
            Total = groups.Sum(g => g.Results.Count),
            Groups = groups
        };
    }

    private static string DayOfWeekIso(DateOnly date) => date.ToString("yyyy-MM-dd");

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string Snippet(string text, string query, int length = 140)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var plain = Regex.Replace(text, @"[#*`>\-\[\]!]", " ");
        plain = Regex.Replace(plain, @"\s+", " ").Trim();
        var index = plain.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return plain.Length <= length ? plain : plain[..length] + "…";
        var start = Math.Max(0, index - 40);
        var end = Math.Min(plain.Length, start + length);
        var snippet = plain[start..end];
        return (start > 0 ? "…" : "") + snippet + (end < plain.Length ? "…" : "");
    }
}
