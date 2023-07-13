namespace UrlShortener.Services;

public class CleanupOptions
{
    public readonly static TimeSpan DefaultInterval = TimeSpan.FromMinutes(30);

    public TimeSpan? Interval { get; set; } = DefaultInterval;

    public bool LogOnly { get; set; } = false;
}