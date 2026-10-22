using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LearningHub.Api.Data;
using LearningHub.Core.Models;
using LearningHub.Core.Util;
using Microsoft.EntityFrameworkCore;

namespace LearningHub.Api.Services;

public sealed class GoogleCalendarStatusDto
{
    public bool Configured { get; set; }
    public bool Connected { get; set; }
    public string? Email { get; set; }
    public string Message { get; set; } = "";
}

public sealed class GooglePushResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Optional Google Calendar push. It is deliberately inert until GOOGLE_CLIENT_ID and
/// GOOGLE_CLIENT_SECRET are configured, and nothing depends on it.
/// </summary>
public sealed class GoogleCalendarService(
    HubDbContext db,
    ScheduleService schedules,
    IHttpClientFactory httpFactory,
    ILogger<GoogleCalendarService> logger)
{
    private static readonly ConcurrentDictionary<string, DateTime> PendingStates = new();
    private const string Scope = "https://www.googleapis.com/auth/calendar.events";

    private static string ClientId => Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "";
    private static string ClientSecret => Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? "";
    private static string RedirectUri =>
        Environment.GetEnvironmentVariable("GOOGLE_REDIRECT_URI")
        ?? "http://localhost:5180/api/calendar/google/callback";
    private static bool Configured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public async Task<GoogleCalendarStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var token = await db.GoogleTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == 1, ct);
        return new GoogleCalendarStatusDto
        {
            Configured = Configured,
            Connected = token is not null && !string.IsNullOrWhiteSpace(token.RefreshToken),
            Email = token?.Email,
            Message = !Configured
                ? "Google Calendar is off. Set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET to enable the optional connection."
                : token is null
                    ? "Not connected. The app works fully without it."
                    : "Connected. Use Push schedule to send planned lessons."
        };
    }

    public string BuildAuthUrl()
    {
        if (!Configured)
            throw new InvalidOperationException("Google Calendar is not configured on this machine.");

        var state = Guid.NewGuid().ToString("N");
        PendingStates[state] = DateTime.UtcNow.AddMinutes(15);
        var query = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["response_type"] = "code",
            ["scope"] = Scope,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state
        };
        return "https://accounts.google.com/o/oauth2/v2/auth?" +
               string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<string> HandleCallbackAsync(string code, string state, CancellationToken ct = default)
    {
        if (!PendingStates.TryRemove(state, out var expires) || expires < DateTime.UtcNow)
            throw new InvalidOperationException("The Google sign-in attempt expired. Please try connecting again.");

        var client = httpFactory.CreateClient("google");
        var tokenResponse = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["redirect_uri"] = RedirectUri,
                ["grant_type"] = "authorization_code"
            }), ct);
        var body = await tokenResponse.Content.ReadAsStringAsync(ct);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Google token exchange failed: {Body}", body);
            throw new InvalidOperationException("Google rejected the connection. Check the OAuth client and redirect URI.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString() ?? "";
        var refreshToken = root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() ?? "" : "";
        var expiresIn = root.TryGetProperty("expires_in", out var expiresInElement) ? expiresInElement.GetInt32() : 3600;

        var token = await db.GoogleTokens.FirstOrDefaultAsync(t => t.Id == 1, ct);
        if (token is null)
        {
            token = new GoogleToken { Id = 1 };
            db.GoogleTokens.Add(token);
        }
        token.AccessToken = accessToken;
        if (!string.IsNullOrWhiteSpace(refreshToken))
            token.RefreshToken = refreshToken;
        token.ExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
        await db.SaveChangesAsync(ct);

        return "/settings?calendar=connected";
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        var token = await db.GoogleTokens.FirstOrDefaultAsync(t => t.Id == 1, ct);
        if (token is null) return;
        db.GoogleTokens.Remove(token);
        await db.SaveChangesAsync(ct);
    }

    public async Task<GooglePushResult> PushAsync(CancellationToken ct = default)
    {
        if (!Configured)
            throw new InvalidOperationException("Google Calendar is not configured. Set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET first.");

        var token = await db.GoogleTokens.FirstOrDefaultAsync(t => t.Id == 1, ct)
                    ?? throw new InvalidOperationException("Google Calendar is not connected yet.");
        var accessToken = await EnsureAccessTokenAsync(token, ct);
        var today = AppClock.Today;
        await schedules.RebuildAsync(today, ct: ct);

        var entries = await db.PlanEntries.AsNoTracking()
            .Where(e => e.Date >= today && e.Status != PlanEntryStatus.Missed)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);
        var lessons = await db.Lessons.AsNoTracking().ToDictionaryAsync(l => l.Id, ct);
        var tracks = await db.Tracks.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
        var skills = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);

        var client = httpFactory.CreateClient("google");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var existing = await ListEventsAsync(client, today, ct);
        var result = new GooglePushResult();
        var keepKeys = new HashSet<string>();

        foreach (var entry in entries)
        {
            var track = tracks.GetValueOrDefault(entry.TrackId);
            var skillName = track is null ? "" : skills.GetValueOrDefault(track.SkillId)?.Name ?? track.SkillId;
            var lessonTitle = lessons.GetValueOrDefault(entry.LessonId)?.Title ?? entry.LessonId;
            var key = $"{entry.TrackId}:{entry.LessonId}:{entry.Date:yyyyMMdd}";
            keepKeys.Add(key);

            var payload = new
            {
                summary = $"{skillName}: {lessonTitle}",
                description = $"{entry.Minutes} min study slice · Learning Hub",
                start = new { date = entry.Date.ToString("yyyy-MM-dd") },
                end = new { date = entry.Date.AddDays(1).ToString("yyyy-MM-dd") },
                extendedProperties = new { @private = new Dictionary<string, string> { ["learningHub"] = "1", ["entryKey"] = key } }
            };
            var json = JsonSerializer.Serialize(payload);

            if (existing.TryGetValue(key, out var eventId))
            {
                var update = await client.PutAsync(
                    $"https://www.googleapis.com/calendar/v3/calendars/primary/events/{Uri.EscapeDataString(eventId)}",
                    new StringContent(json, Encoding.UTF8, "application/json"), ct);
                if (update.IsSuccessStatusCode) result.Updated++;
            }
            else
            {
                var create = await client.PostAsync(
                    "https://www.googleapis.com/calendar/v3/calendars/primary/events",
                    new StringContent(json, Encoding.UTF8, "application/json"), ct);
                if (create.IsSuccessStatusCode) result.Created++;
            }
        }

        foreach (var (key, eventId) in existing)
        {
            if (keepKeys.Contains(key)) continue;
            var delete = await client.DeleteAsync(
                $"https://www.googleapis.com/calendar/v3/calendars/primary/events/{Uri.EscapeDataString(eventId)}", ct);
            if (delete.IsSuccessStatusCode) result.Deleted++;
        }

        result.Message = $"Pushed {result.Created} new, updated {result.Updated}, removed {result.Deleted} stale event(s).";
        return result;
    }

    private async Task<Dictionary<string, string>> ListEventsAsync(
        HttpClient client,
        DateOnly from,
        CancellationToken ct)
    {
        var timeMin = from.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var timeMax = from.AddYears(2).ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var url = "https://www.googleapis.com/calendar/v3/calendars/primary/events" +
                  $"?privateExtendedProperty={Uri.EscapeDataString("learningHub=1")}" +
                  $"&timeMin={Uri.EscapeDataString(timeMin)}&timeMax={Uri.EscapeDataString(timeMax)}" +
                  "&singleEvents=true&maxResults=2500";
        var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Google Calendar list failed: {Body}", body);
            throw new InvalidOperationException("Google Calendar could not list existing events. Try reconnecting.");
        }

        var result = new Dictionary<string, string>();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("items", out var items)) return result;
        foreach (var item in items.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var key = item.TryGetProperty("extendedProperties", out var props)
                      && props.TryGetProperty("private", out var priv)
                      && priv.TryGetProperty("entryKey", out var entryKey)
                ? entryKey.GetString()
                : null;
            if (id is not null && key is not null) result[key] = id;
        }
        return result;
    }

    private async Task<string> EnsureAccessTokenAsync(GoogleToken token, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(token.AccessToken) && token.ExpiresAt > DateTime.UtcNow.AddSeconds(30))
            return token.AccessToken;
        if (string.IsNullOrWhiteSpace(token.RefreshToken))
            throw new InvalidOperationException("Google Calendar needs to be reconnected.");

        var client = httpFactory.CreateClient("google");
        var response = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["refresh_token"] = token.RefreshToken,
                ["grant_type"] = "refresh_token"
            }), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Google token refresh failed: {Body}", body);
            throw new InvalidOperationException("Google Calendar authorization expired. Please reconnect.");
        }

        using var doc = JsonDocument.Parse(body);
        token.AccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
        token.ExpiresAt = DateTime.UtcNow.AddSeconds(3500);
        await db.SaveChangesAsync(ct);
        return token.AccessToken;
    }
}
