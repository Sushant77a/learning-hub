namespace LearningHub.Core.Util;

public static class AppClock
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
    public static DateTime Now => DateTime.Now;
}

public static class Days
{
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }

    public static IEnumerable<DateOnly> Range(DateOnly start, DateOnly endInclusive)
    {
        for (var day = start; day <= endInclusive; day = day.AddDays(1))
            yield return day;
    }

    public static string Short(DateOnly date) => date.ToString("d MMM");
    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");
}

public static class NameUtil
{
    public static string Slug(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}
