namespace Boatdrill.Core.Text;

/// <summary>Numbers the way a person says them. Everything the app shows goes through here.</summary>
public static class Humanize
{
    private static readonly string[] Units = ["bytes", "KB", "MB", "GB", "TB", "PB"];

    public static string Bytes(long bytes)
    {
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes:N0} bytes" : $"{value:0.#} {Units[unit]}";
    }

    /// <summary>"45 s", "12 min", "2 h 10 min", "3 days": how long something takes.</summary>
    public static string Duration(TimeSpan span)
    {
        if (span.TotalSeconds < 60) return $"{Math.Max(1, (int)Math.Round(span.TotalSeconds))} s";
        if (span.TotalMinutes < 60) return $"{(int)Math.Round(span.TotalMinutes)} min";
        if (span.TotalHours < 48)
        {
            var hours = (int)span.TotalHours;
            return span.Minutes == 0 ? $"{hours} h" : $"{hours} h {span.Minutes} min";
        }

        return $"{(int)Math.Round(span.TotalDays)} days";
    }

    /// <summary>"3 minutes", "1 hour", "9 days", "2 months": how long ago something was.</summary>
    public static string Age(TimeSpan span)
    {
        if (span.TotalMinutes < 1) return "less than a minute";
        if (span.TotalHours < 1) return Plural((int)Math.Round(span.TotalMinutes), "minute");
        if (span.TotalDays < 1) return Plural((int)Math.Round(span.TotalHours), "hour");
        if (span.TotalDays < 60) return Plural((int)Math.Round(span.TotalDays), "day");
        if (span.TotalDays < 730) return Plural((int)Math.Round(span.TotalDays / 30.4), "month");
        return Plural((int)Math.Round(span.TotalDays / 365.25), "year");
    }

    public static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count:N0} {noun}s";
}
