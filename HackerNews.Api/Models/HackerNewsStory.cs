namespace HackerNews.Api.Models;

/// <summary>
/// Represents a Hacker News story as returned by the API.
/// </summary>
public class HackerNewsStory
{
    /// <summary>
    /// The story's unique ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The title of the story.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The URL of the story.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// The username of the submitter.
    /// </summary>
    public string? By { get; set; }

    /// <summary>
    /// Creation date of the story (Unix timestamp).
    /// </summary>
    public long Time { get; set; }

    /// <summary>
    /// The story's score, or the votes for it.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// The IDs of the item's comments, in ranked display order.
    /// </summary>
    public int[]? Kids { get; set; }

    /// <summary>
    /// The type of item (story, comment, etc.).
    /// </summary>
    public string? Type { get; set; }
}
