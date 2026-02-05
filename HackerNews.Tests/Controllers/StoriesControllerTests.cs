using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using HackerNews.Api.Controllers;
using HackerNews.Api.Models;
using HackerNews.Api.Services;

namespace HackerNews.Tests.Controllers;

public class StoriesControllerTests
{
    private readonly Mock<IHackerNewsService> _mockService;
    private readonly Mock<ILogger<StoriesController>> _mockLogger;
    private readonly StoriesController _controller;

    public StoriesControllerTests()
    {
        _mockService = new Mock<IHackerNewsService>();
        _mockLogger = new Mock<ILogger<StoriesController>>();
        _controller = new StoriesController(_mockService.Object, _mockLogger.Object);
        
        // Set up HttpContext for ProblemDetails
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetBestStories_WithValidCount_ReturnsOkResult()
    {
        // Arrange
        var expectedStories = new List<StoryResponse>
        {
            new() { Title = "Story 1", Score = 100, Uri = "http://example.com/1", PostedBy = "user1", Time = "2021-01-01T00:00:00Z", CommentCount = 10 },
            new() { Title = "Story 2", Score = 90, Uri = "http://example.com/2", PostedBy = "user2", Time = "2021-01-02T00:00:00Z", CommentCount = 5 }
        };

        _mockService
            .Setup(s => s.GetBestStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStories);

        // Act
        var result = await _controller.GetBestStories(10);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedStories);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    [InlineData(1000)]
    public async Task GetBestStories_WithInvalidCount_ReturnsBadRequest(int count)
    {
        // Act
        var result = await _controller.GetBestStories(count);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult!.Value.Should().BeOfType<ProblemDetails>();
        
        var problemDetails = badRequestResult.Value as ProblemDetails;
        problemDetails!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Title.Should().Be("Invalid count parameter");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public async Task GetBestStories_WithValidBoundaryValues_ReturnsOkResult(int count)
    {
        // Arrange
        var expectedStories = new List<StoryResponse>
        {
            new() { Title = "Story 1", Score = 100, Uri = "http://example.com/1", PostedBy = "user1", Time = "2021-01-01T00:00:00Z", CommentCount = 10 }
        };

        _mockService
            .Setup(s => s.GetBestStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStories);

        // Act
        var result = await _controller.GetBestStories(count);
        
        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBestStories_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetBestStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetBestStories(10);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        objectResult.Value.Should().BeOfType<ProblemDetails>();
        
        var problemDetails = objectResult.Value as ProblemDetails;
        problemDetails!.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Title.Should().Be("An error occurred while processing your request");
    }

    [Fact]
    public async Task GetBestStories_WithDefaultCount_UsesDefaultValue()
    {
        // Arrange
        var expectedStories = new List<StoryResponse>
        {
            new() { Title = "Story 1", Score = 100, Uri = "http://example.com/1", PostedBy = "user1", Time = "2021-01-01T00:00:00Z", CommentCount = 10 }
        };

        _mockService
            .Setup(s => s.GetBestStoriesAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStories);

        // Act
        var result = await _controller.GetBestStories();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _mockService.Verify(s => s.GetBestStoriesAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBestStories_WithEmptyResult_ReturnsOkWithEmptyList()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetBestStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StoryResponse>());

        // Act
        var result = await _controller.GetBestStories(10);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var stories = okResult!.Value as IEnumerable<StoryResponse>;
        stories.Should().NotBeNull();
        stories.Should().BeEmpty();
    }
}
