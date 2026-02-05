namespace HackerNews.Api.Configuration;

/// <summary>
/// Configuration settings for caching.
/// </summary>
public class CacheSettings
{
    /// <summary>
    /// The section name in appsettings.json.
    /// </summary>
    public const string SectionName = "CacheSettings";

    /// <summary>
    /// Expiration time in seconds for cached story IDs.
    /// </summary>
    public int StoryIdsExpirationSeconds { get; set; } = 300;

    /// <summary>
    /// Expiration time in seconds for cached story details.
    /// </summary>
    public int StoryDetailsExpirationSeconds { get; set; } = 600;

    /// <summary>
    /// Expiration time in seconds for stale cache entries (fallback during circuit breaker open state).
    /// Should be significantly longer than regular expiration to provide fallback during outages.
    /// </summary>
    public int StaleCacheExpirationSeconds { get; set; } = 3600;
}
