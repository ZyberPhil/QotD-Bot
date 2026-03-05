namespace QotD.Bot.Configuration;

/// <summary>Settings for the daily scheduling logic.</summary>
public sealed class SchedulingSettings
{
    public const string SectionName = "Scheduling";

    /// <summary>
    /// Time of day (in the configured Timezone) at which the question is posted.
    /// Format: "HH:mm" (24-hour), e.g. "07:00".
    /// </summary>
    public string PostTime { get; set; } = "07:00";

    /// <summary>
    /// IANA timezone name used to interpret PostTime, e.g. "Europe/Berlin".
    /// The application will convert this to UTC internally.
    /// </summary>
    public string Timezone { get; set; } = "Europe/Berlin";

    /// <summary>Parses PostTime into a <see cref="TimeOnly"/> value.</summary>
    public TimeOnly GetPostTime() => TimeOnly.ParseExact(PostTime, "HH:mm");

    /// <summary>Resolves the configured IANA timezone.</summary>
    public TimeZoneInfo GetTimeZoneInfo() => TimeZoneInfo.FindSystemTimeZoneById(Timezone);
}
