namespace HackerNews.Api.Models;

/// <summary>
/// Represents a Hacker News story response.
/// </summary>
public class StoryResponse
{
    /// <summary>
    /// The title of the story.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The URL of the story.
    /// </summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// The username of the person who posted the story.
    /// </summary>
    public string PostedBy { get; set; } = string.Empty;

    /// <summary>
    /// The time the story was posted (ISO 8601 format).
    /// </summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>
    /// The story's score.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// The number of comments on the story.
    /// </summary>
    public int CommentCount { get; set; }
}
