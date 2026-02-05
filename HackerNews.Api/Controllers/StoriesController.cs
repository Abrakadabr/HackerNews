using Microsoft.AspNetCore.Mvc;
using HackerNews.Api.Models;
using HackerNews.Api.Services;

namespace HackerNews.Api.Controllers;

/// <summary>
/// API controller for retrieving Hacker News stories.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StoriesController : ControllerBase
{
    private readonly IHackerNewsService _hackerNewsService;
    private readonly ILogger<StoriesController> _logger;

    private const int MinCount = 1;
    private const int MaxCount = 500;

    /// <summary>
    /// Initializes a new instance of the StoriesController.
    /// </summary>
    public StoriesController(
        IHackerNewsService hackerNewsService,
        ILogger<StoriesController> logger)
    {
        _hackerNewsService = hackerNewsService ?? throw new ArgumentNullException(nameof(hackerNewsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves the best stories from Hacker News.
    /// </summary>
    /// <param name="count">The number of best stories to retrieve (1-500).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of the best stories sorted by score in descending order.</returns>
    /// <response code="200">Returns the list of best stories.</response>
    /// <response code="400">If the count parameter is invalid.</response>
    /// <response code="500">If an internal server error occurs.</response>
    [HttpGet("best")]
    [ProducesResponseType(typeof(IEnumerable<StoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<StoryResponse>>> GetBestStories(
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        // Validate count parameter
        if (count < MinCount || count > MaxCount)
        {
            _logger.LogWarning("Invalid count parameter: {Count}. Must be between {Min} and {Max}", 
                count, MinCount, MaxCount);
            
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid count parameter",
                Detail = $"The count parameter must be between {MinCount} and {MaxCount}.",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            _logger.LogInformation("Retrieving {Count} best stories", count);

            var stories = (await _hackerNewsService.GetBestStoriesAsync(count, cancellationToken)).ToList();

            _logger.LogInformation("Successfully retrieved {Count} best stories", stories.Count);

            return Ok(stories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving best stories");

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred while processing your request",
                Detail = "Unable to retrieve stories from Hacker News. Please try again later.",
                Instance = HttpContext.Request.Path
            });
        }
    }
}
