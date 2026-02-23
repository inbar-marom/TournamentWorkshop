namespace TournamentEngine.Core.Utilities;

/// <summary>
/// Helper for timezone conversions, focusing on Israel Standard Time.
/// </summary>
public static class TimezoneHelper
{
    private static readonly TimeZoneInfo IsraelTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time");

    /// <summary>
    /// Converts UTC DateTime to Israel time.
    /// </summary>
    public static DateTime ToIsraelTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Input DateTime must be in UTC", nameof(utcDateTime));

        return TimeZoneInfo.ConvertTime(utcDateTime, IsraelTimeZone);
    }

    /// <summary>
    /// Converts a local DateTime to Israel time (assumes input is local system time).
    /// </summary>
    public static DateTime LocalToIsraelTime(DateTime localDateTime)
    {
        // Convert local to UTC first
        var utc = localDateTime.Kind == DateTimeKind.Local 
            ? localDateTime.ToUniversalTime() 
            : localDateTime;
        return ToIsraelTime(utc);
    }

    /// <summary>
    /// Gets current time in Israel timezone.
    /// </summary>
    public static DateTime GetNowIsrael()
    {
        return ToIsraelTime(DateTime.UtcNow);
    }

    /// <summary>
    /// Formats a UTC DateTime as an ISO 8601 string in Israel timezone.
    /// </summary>
    public static string FormatIsraelTime(DateTime utcDateTime)
    {
        var israelTime = ToIsraelTime(utcDateTime);
        return israelTime.ToString("O");
    }

    /// <summary>
    /// Formats a UTC DateTime for CSV export in Israel timezone.
    /// </summary>
    public static string FormatIsraelTimeForCsv(DateTime utcDateTime)
    {
        var israelTime = ToIsraelTime(utcDateTime);
        return israelTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
