namespace HackerNews.Api.Configuration;

/// <summary>
/// Configuration settings for the Hacker News API.
/// </summary>
public class HackerNewsApiSettings
{
    /// <summary>
    /// The section name in appsettings.json.
    /// </summary>
    public const string SectionName = "HackerNewsApi";

    /// <summary>
    /// Base URL for the Hacker News API (without trailing slash).
    /// </summary>
    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0";

    /// <summary>
    /// Maximum number of concurrent requests to the Hacker News API.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;
}
