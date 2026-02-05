using HackerNews.Api.Models;

namespace HackerNews.Api.Services;

/// <summary>
/// Service interface for interacting with the Hacker News API.
/// </summary>
public interface IHackerNewsService
{
    /// <summary>
    /// Retrieves the best stories from Hacker News.
    /// </summary>
    /// <param name="count">The number of stories to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of story responses sorted by score in descending order.</returns>
    Task<IEnumerable<StoryResponse>> GetBestStoriesAsync(int count, CancellationToken cancellationToken = default);
}
