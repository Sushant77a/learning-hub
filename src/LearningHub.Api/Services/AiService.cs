using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Core.Curriculum;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class AiUnavailableException(string message) : Exception(message);

public sealed class AiStatusDto
{
    public bool Configured { get; set; }
    public string Provider { get; set; } = "openai";
    public string Model { get; set; } = "";
    public string? BaseUrl { get; set; }
    public string KeyStatus { get; set; } = "missing";
    public string Message { get; set; } = "";
}

public sealed class AiGradeDto
{
    public int Score { get; set; }
    public string Verdict { get; set; } = "";
    public List<string> Strengths { get; set; } = new();
    public List<string> Gaps { get; set; } = new();
    public List<string> Review { get; set; } = new();
    public string? Raw { get; set; }
}

public sealed class AiOptions
{
    public string Provider { get; init; } = "openai";
    public string Model { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string BaseUrl { get; init; } = "";
    public bool Configured { get; init; }
}

public sealed class AiService(
    HubDbContext db,
    IConfiguration configuration,
    CurriculumService curriculum,
    ScheduleService schedules,
    IHttpClientFactory httpFactory,
    ILogger<AiService> logger)
{
    public async Task<AiStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var options = await ResolveOptionsAsync(ct);
        return new AiStatusDto
        {
            Configured = options.Configured,
            Provider = options.Provider,
            Model = options.Model,
            BaseUrl = options.Provider == "openai" || options.Provider == "anthropic" ? null : options.BaseUrl,
            KeyStatus = options.Provider == "ollama"
                ? "not required (local)"
                : string.IsNullOrWhiteSpace(options.ApiKey) ? "missing" : "set in environment",
            Message = options.Configured
                ? $"{ProviderLabel(options.Provider)} · {options.Model}"
                : "AI is off. Set AI_API_KEY in the environment (or run Ollama locally) to enable on-demand help."
        };
    }

    private static string ProviderLabel(string provider) => provider switch
    {
        "anthropic" => "Anthropic",
        "ollama" => "Ollama (local)",
        "openai-compatible" => "OpenAI-compatible",
        _ => "OpenAI"
    };

    private async Task<AiOptions> ResolveOptionsAsync(CancellationToken ct)
    {
        var settings = await schedules.GetSettingsAsync(ct);
        var provider = (settings.AiProviderOverride
                        ?? Environment.GetEnvironmentVariable("AI_PROVIDER")
                        ?? configuration["Ai:Provider"]
                        ?? "openai").Trim().ToLowerInvariant();
        var apiKey = Environment.GetEnvironmentVariable("AI_API_KEY")
                     ?? configuration["Ai:ApiKey"]
                     ?? "";
        var baseUrl = (Environment.GetEnvironmentVariable("AI_BASE_URL")
                     ?? configuration["Ai:BaseUrl"]
                     ?? "").TrimEnd('/');
        var model = Environment.GetEnvironmentVariable("AI_MODEL")
                  ?? configuration["Ai:Model"]
                  ?? "";

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = provider switch
            {
                "anthropic" => "https://api.anthropic.com",
                "ollama" => "http://localhost:11434/v1",
                _ => "https://api.openai.com/v1"
            };
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            model = provider switch
            {
                "anthropic" => "claude-sonnet-4-5",
                "ollama" => "qwen2.5-coder:7b",
                _ => "gpt-4.1-mini"
            };
        }

        var configured = provider == "ollama"
            ? !string.IsNullOrWhiteSpace(baseUrl)
            : !string.IsNullOrWhiteSpace(apiKey);

        return new AiOptions
        {
            Provider = provider,
            Model = model,
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            Configured = configured
        };
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
        => (await ResolveOptionsAsync(ct)).Configured;

    public async Task<string> ExplainDifferentlyAsync(string lessonId, string? focus, CancellationToken ct = default)
    {
        var lesson = await LoadLessonAsync(lessonId, ct);
        var user = $"""
            The learner is studying: {lesson.Title} ({lesson.Skill}).
            {LessonContext(lesson)}

            Explain this lesson topic differently from the lesson text. Use one small, concrete C# or .NET
            example where it helps. Focus on the mental model, not a summary. Keep it under 350 words.
            {(string.IsNullOrWhiteSpace(focus) ? "" : $"The learner specifically asks: {focus}")}
            """;
        return await CompleteAsync(
            "You are a patient, precise tutor for C#/.NET and computer science. Explain clearly, avoid filler, use Markdown.",
            user, ct);
    }

    public async Task<List<Question>> MorePracticeAsync(string lessonId, CancellationToken ct = default)
    {
        var lesson = await LoadLessonAsync(lessonId, ct);
        var user = $$"""
            Generate exactly 3 new multiple-choice questions for this lesson.
            {{LessonContext(lesson)}}

            Return ONLY JSON in this shape:
            {"questions": [{"prompt": "...", "options": ["...", "...", "...", "..."], "correctIndex": 0, "explanation": "...", "optionExplanations": ["...", "...", "...", "..."]}]}
            Every question must have 4 options, one correct index, and an explanation for every option.
            Questions must test understanding, not trivia.
            """;
        var raw = await CompleteAsync(
            "You write rigorous, fair multiple-choice questions. Return only valid JSON, no prose.",
            user, ct);
        var json = ExtractJson(raw, '{');
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("questions", out var questionsElement)
            || questionsElement.ValueKind != JsonValueKind.Array)
            throw new AiUnavailableException("The AI returned an unexpected format. Please try again.");

        var created = new List<Question>();
        foreach (var item in questionsElement.EnumerateArray())
        {
            var prompt = item.TryGetProperty("prompt", out var p) ? p.GetString() ?? "" : "";
            var options = item.TryGetProperty("options", out var o)
                ? o.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).Take(6).ToList()
                : new List<string>();
            var correct = item.TryGetProperty("correctIndex", out var c) && c.TryGetInt32(out var ci) ? ci : -1;
            var explanation = item.TryGetProperty("explanation", out var e) ? e.GetString() ?? "" : "";
            var optionExplanations = item.TryGetProperty("optionExplanations", out var oe)
                ? oe.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                : new List<string>();
            if (string.IsNullOrWhiteSpace(prompt) || options.Count < 2 || correct < 0 || correct >= options.Count)
                continue;

            var question = new Question
            {
                SkillId = lesson.Skill,
                LessonId = lesson.Id,
                Kind = QuestionKind.Custom,
                Prompt = prompt,
                OptionsJson = JsonSerializer.Serialize(options),
                CorrectIndex = correct,
                Explanation = explanation,
                OptionExplanationsJson = JsonSerializer.Serialize(optionExplanations),
                IsCustom = true
            };
            db.Questions.Add(question);
            created.Add(question);
        }

        await db.SaveChangesAsync(ct);
        if (created.Count == 0)
            throw new AiUnavailableException("The AI did not return usable questions. Please try again.");
        return created;
    }

    public async Task<AiGradeDto> GradeAsync(
        string lessonId,
        int trackId,
        string answer,
        CancellationToken ct = default)
    {
        var lesson = await LoadLessonAsync(lessonId, ct);
        var user = $$"""
            Lesson: {{lesson.Title}} ({{lesson.Skill}}).
            {{LessonContext(lesson)}}

            The learner explained it back in their own words:
            ---
            {{answer}}
            ---

            Grade for correctness and gaps. Return ONLY JSON:
            {"score": 1-5, "verdict": "one sentence", "strengths": ["..."], "gaps": ["..."], "review": ["short review actions"]}
            Be encouraging but honest. If a key idea is missing, say exactly what to revisit.
            """;
        var raw = await CompleteAsync(
            "You are a careful tutor grading a learner's explanation. Return only valid JSON.",
            user, ct);
        var json = ExtractJson(raw, '{');
        AiGradeDto grade;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            grade = new AiGradeDto
            {
                Score = root.TryGetProperty("score", out var score) && score.TryGetInt32(out var s)
                    ? Math.Clamp(s, 1, 5)
                    : 3,
                Verdict = root.TryGetProperty("verdict", out var verdict) ? verdict.GetString() ?? "" : "",
                Strengths = ReadStringArray(root, "strengths"),
                Gaps = ReadStringArray(root, "gaps"),
                Review = ReadStringArray(root, "review"),
                Raw = raw
            };
        }
        catch (JsonException)
        {
            grade = new AiGradeDto { Score = 3, Verdict = raw, Raw = raw };
        }

        var progress = await db.LessonProgress
            .FirstOrDefaultAsync(p => p.LessonId == lessonId && p.TrackId == trackId, ct);
        if (progress is not null)
        {
            progress.ExplainBack = answer;
            progress.ExplainGrade = JsonSerializer.Serialize(grade);
            progress.LastActivityAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return grade;
    }

    public async Task<string> WhyCodeWrongAsync(
        string lessonId,
        string code,
        string? error,
        CancellationToken ct = default)
    {
        var lesson = await LoadLessonAsync(lessonId, ct);
        var user = $"""
            Lesson: {lesson.Title} ({lesson.Skill}).
            {LessonContext(lesson)}

            The learner's code:
            ```csharp
            {code}
            ```
            {(string.IsNullOrWhiteSpace(error) ? "They are not sure what is wrong." : $"Reported problem: {error}")}

            Point out the bug, explain why it happens, and give one hint before the corrected snippet.
            Do not rewrite their whole program. Keep it under 300 words.
            """;
        return await CompleteAsync(
            "You are a debugging tutor. Diagnose precisely, teach the underlying rule, and avoid taking over the problem.",
            user, ct);
    }

    public async Task<List<LessonFile>> GenerateMiniCurriculumAsync(
        string skillId,
        int days,
        int minutesPerDay,
        CancellationToken ct = default)
    {
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == skillId, ct)
                    ?? throw new KeyNotFoundException("Skill not found.");
        var lessonCount = Math.Clamp(days, 2, 8);
        var existing = await db.Lessons.Where(l => l.SkillId == skillId).Select(l => l.Title).ToListAsync(ct);
        var existingText = existing.Count == 0
            ? "There are no lessons yet."
            : $"Existing lessons (do not duplicate): {string.Join("; ", existing)}.";

        var user = $$"""
            Create a focused mini-curriculum for the skill "{{skill.Name}}" ({{skill.Description}}).
            The learner has {{lessonCount}} study days and about {{minutesPerDay}} minutes per day.
            {{existingText}}

            Return ONLY a JSON object with a "lessons" array of exactly {{lessonCount}} lessons.
            Each lesson must use this shape:
            {
              "id": "kebab-case-id",
              "skill": "{{skillId}}",
              "order": 1,
              "title": "...",
              "summary": "one sentence",
              "estimatedMinutes": {{Math.Clamp(minutesPerDay, 15, 120)}},
              "theory": "Markdown theory with short code examples. 250-450 words.",
              "workedExample": {"title": "...", "markdown": "...", "steps": ["...", "..."]},
              "checkQuestions": [{"prompt":"...", "options":["...","...","...","..."], "correctIndex":0, "explanation":"...", "optionExplanations":["...","...","...","..."]}],
              "explainPrompt": "Ask the learner to explain the core idea in their own words.",
              "recallQuestions": []
            }
            Include 2 checkQuestions per lesson, each with four options and explanations for all options.
            Theory first; include a short coding task ONLY if the lesson truly needs one, using the "task" key.
            No phases, no filler. Order lessons so each builds on the previous one.
            """;
        var raw = await CompleteAsync(
            "You design compact, rigorous curricula. Return only valid JSON, no prose.",
            user, ct);
        var json = ExtractJson(raw, '{');
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("lessons", out var lessonsElement)
            || lessonsElement.ValueKind != JsonValueKind.Array)
            throw new AiUnavailableException("The AI returned an unexpected curriculum format. Please try again.");

        var generated = new List<LessonFile>();
        var order = 1;
        foreach (var element in lessonsElement.EnumerateArray().Take(lessonCount))
        {
            var lessonJson = element.GetRawText();
            var lesson = JsonSerializer.Deserialize<LessonFile>(lessonJson, CurriculumJson.Options);
            if (lesson is null) continue;
            lesson.Skill = skillId;
            lesson.Order = order;
            lesson.Id = $"{skillId}-mini-{order:00}-{NameUtil.Slug(lesson.Title)}";
            lesson.EstimatedMinutes = Math.Clamp(lesson.EstimatedMinutes > 0 ? lesson.EstimatedMinutes : minutesPerDay, 15, 120);
            if (lesson.CheckQuestions.Count == 0)
            {
                lesson.CheckQuestions.Add(new CheckQuestion
                {
                    Prompt = $"What is the core idea of {lesson.Title}?",
                    Options = new List<string> { "It is explained in the lesson.", "Not covered.", "Skip it.", "None." },
                    CorrectIndex = 0,
                    Explanation = "Review the theory section for the core idea."
                });
            }
            var validation = CurriculumJson.ParseLesson(
                JsonSerializer.Serialize(lesson, CurriculumJson.Options),
                $"generated/{lesson.Id}.json",
                new[] { skillId });
            if (!validation.IsValid || validation.Lesson is null)
            {
                logger.LogWarning("AI lesson rejected: {Errors}", string.Join("; ", validation.Errors));
                continue;
            }

            generated.Add(validation.Lesson);
            order++;
        }

        if (generated.Count == 0)
            throw new AiUnavailableException("The AI did not return a usable curriculum. Please try again.");

        foreach (var lesson in generated)
            await curriculum.WriteGeneratedLessonAsync(lesson, ct);
        await curriculum.SyncAsync(db, ct);
        return generated;
    }

    public async Task<List<ReflectionSuggestion>> SuggestPlanAdjustmentsAsync(
        int reflectionId,
        CancellationToken ct = default)
    {
        var reflection = await db.Reflections.FirstOrDefaultAsync(r => r.Id == reflectionId, ct)
                         ?? throw new KeyNotFoundException("Reflection not found.");
        var user = $$"""
            Weekly reflection:
            What went well: {{reflection.WentWell}}
            What was hard: {{reflection.WasHard}}
            What to change: {{reflection.ToChange}}
            Summary data: {{reflection.SummaryJson}}

            Suggest 2-3 concrete, small plan adjustments for next week. Return ONLY JSON:
            {"suggestions": [{"text": "specific adjustment", "rationale": "why"}]}
            """;
        var raw = await CompleteAsync(
            "You coach a self-directed learner. Suggest concrete, kind, realistic adjustments. Return only JSON.",
            user, ct);
        var json = ExtractJson(raw, '{');
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("suggestions", out var suggestions)
            || suggestions.ValueKind != JsonValueKind.Array)
            throw new AiUnavailableException("The AI returned an unexpected format. Please try again.");

        var created = new List<ReflectionSuggestion>();
        foreach (var item in suggestions.EnumerateArray().Take(4))
        {
            var text = item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? ""
                : item.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(text)) continue;
            var rationale = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("rationale", out var r)
                ? r.GetString()
                : null;
            var suggestion = new ReflectionSuggestion
            {
                ReflectionId = reflectionId,
                Text = text,
                Rationale = rationale,
                State = SuggestionState.Pending
            };
            db.ReflectionSuggestions.Add(suggestion);
            created.Add(suggestion);
        }
        await db.SaveChangesAsync(ct);
        if (created.Count == 0)
            throw new AiUnavailableException("The AI did not return suggestions. Please try again.");
        return created;
    }

    private async Task<LessonFile> LoadLessonAsync(string lessonId, CancellationToken ct)
    {
        var lesson = await db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonId, ct)
                     ?? throw new KeyNotFoundException("Lesson not found.");
        return LessonContent.Get(lesson)
               ?? throw new InvalidOperationException("This lesson could not be parsed.");
    }

    private static string LessonContext(LessonFile lesson)
    {
        var theory = lesson.Theory.Length > 5000 ? lesson.Theory[..5000] : lesson.Theory;
        var checks = string.Join("\n", lesson.CheckQuestions.Select(q => $"- {q.Prompt}"));
        return $"""
            Lesson summary: {lesson.Summary}
            Theory:
            {theory}
            Check questions:
            {checks}
            """;
    }

    private async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        var options = await ResolveOptionsAsync(ct);
        if (!options.Configured)
            throw new AiUnavailableException(
                "AI is not configured. Set AI_API_KEY in your environment and restart the app, or use a local Ollama model. Everything else works without AI.");

        var client = httpFactory.CreateClient("ai");
        client.Timeout = TimeSpan.FromSeconds(75);

        try
        {
            if (options.Provider == "anthropic")
            {
                var payload = new
                {
                    model = options.Model,
                    max_tokens = 1600,
                    system,
                    messages = new[] { new { role = "user", content = user } }
                };
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl}/v1/messages");
                request.Headers.Add("x-api-key", options.ApiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await client.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                    throw ProviderError(response.StatusCode, body);
                using var doc = JsonDocument.Parse(body);
                var text = doc.RootElement.GetProperty("content").EnumerateArray()
                    .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "text")
                    .Select(x => x.GetProperty("text").GetString())
                    .FirstOrDefault();
                return text ?? throw new AiUnavailableException("The AI returned an empty response.");
            }
            else
            {
                var payload = new
                {
                    model = options.Model,
                    temperature = 0.4,
                    messages = new object[]
                    {
                        new { role = "system", content = system },
                        new { role = "user", content = user }
                    }
                };
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl}/chat/completions");
                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await client.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                    throw ProviderError(response.StatusCode, body);
                using var doc = JsonDocument.Parse(body);
                var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                return string.IsNullOrWhiteSpace(text)
                    ? throw new AiUnavailableException("The AI returned an empty response.")
                    : text;
            }
        }
        catch (AiUnavailableException)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            throw new AiUnavailableException("The AI request timed out. Check your connection and try again.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "AI request failed");
            throw new AiUnavailableException(
                $"Could not reach the AI provider ({options.Provider}). Check AI_BASE_URL and your connection, then try again.");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "AI response could not be parsed");
            throw new AiUnavailableException("The AI returned an unreadable response. Please try again.");
        }
    }

    private AiUnavailableException ProviderError(System.Net.HttpStatusCode status, string body)
    {
        logger.LogWarning("AI provider returned {Status}: {Body}", (int)status, body.Length > 600 ? body[..600] : body);
        var message = status switch
        {
            System.Net.HttpStatusCode.Unauthorized => "The AI provider rejected the API key. Check AI_API_KEY and restart the app.",
            System.Net.HttpStatusCode.TooManyRequests => "The AI provider is rate limiting requests. Try again in a minute.",
            System.Net.HttpStatusCode.NotFound => "The AI model or endpoint was not found. Check AI_MODEL and AI_BASE_URL.",
            _ => $"The AI provider returned an error ({(int)status}). Try again, or check the server log."
        };
        return new AiUnavailableException(message);
    }

    private static string ExtractJson(string value, char opening)
    {
        var cleaned = value.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            if (firstNewline > 0) cleaned = cleaned[(firstNewline + 1)..];
            var fence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) cleaned = cleaned[..fence];
        }
        var start = cleaned.IndexOf(opening);
        var end = cleaned.LastIndexOf(opening == '{' ? '}' : ']');
        if (start < 0 || end <= start)
            throw new AiUnavailableException("The AI did not return valid JSON. Please try again.");
        return cleaned[start..(end + 1)];
    }

    private static List<string> ReadStringArray(JsonElement root, string property)
        => root.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
            : new List<string>();
}
