using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using HackerNews.Api.Configuration;
using HackerNews.Api.Models;
using HackerNews.Api.Services;

namespace HackerNews.Tests.Services;

public class HackerNewsServiceTests
{
    private readonly Mock<ILogger<HackerNewsService>> _mockLogger;
    private readonly IMemoryCache _memoryCache;
    private readonly HackerNewsApiSettings _apiSettings;
    private readonly CacheSettings _cacheSettings;
    private readonly ResilienceSettings _resilienceSettings;

    public HackerNewsServiceTests()
    {
        _mockLogger = new Mock<ILogger<HackerNewsService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _apiSettings = new HackerNewsApiSettings
        {
            BaseUrl = "https://hacker-news.firebaseio.com/v0",
            MaxConcurrentRequests = 10,
            RequestTimeoutSeconds = 30
        };
        _cacheSettings = new CacheSettings
        {
            StoryIdsExpirationSeconds = 300,
            StoryDetailsExpirationSeconds = 600
        };
        _resilienceSettings = new ResilienceSettings
        {
            TimeoutSeconds = 30,
            CircuitBreakerFailureRatio = 0.5,
            CircuitBreakerSamplingDurationSeconds = 30,
            CircuitBreakerMinimumThroughput = 3,
            CircuitBreakerBreakDurationSeconds = 30
        };
    }

    private static HackerNewsStory CreateTestStory(int id, int score, string? url = null, int[]? kids = null)
    {
        return new HackerNewsStory
        {
            Id = id,
            Title = $"Story {id}",
            Score = score,
            By = $"user{id}",
            Time = 1234567890 + id,
            Url = url ?? $"http://example.com/{id}",
            Kids = kids
        };
    }

    private static Mock<HttpMessageHandler> CreateMockHttpHandler(int[] storyIds, HackerNewsStory[] stories, bool includeStoryIdsEndpoint = true)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        if (includeStoryIdsEndpoint)
        {
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("beststories.json")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(storyIds)
                });
        }

        foreach (var story in stories)
        {
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains($"item/{story.Id}.json")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(story)
                });
        }

        return mockHandler;
    }

    private HackerNewsService CreateService(HttpClient httpClient)
    {
        return new HackerNewsService(
            httpClient,
            _memoryCache,
            _mockLogger.Object,
            Options.Create(_apiSettings),
            Options.Create(_cacheSettings),
            Options.Create(_resilienceSettings));
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsStoriesSortedByScore()
    {
        // Arrange
        var storyIds = new[] { 1, 2, 3 };
        var stories = new[]
        {
            CreateTestStory(1, 100),
            CreateTestStory(2, 200),
            CreateTestStory(3, 150)
        };

        var mockHandler = CreateMockHttpHandler(storyIds, stories);
        var service = CreateService(new HttpClient(mockHandler.Object));

        // Act
        var result = await service.GetBestStoriesAsync(3);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        
        var resultList = result.ToList();
        resultList[0].Score.Should().Be(200); // Highest score first
        resultList[1].Score.Should().Be(150);
        resultList[2].Score.Should().Be(100);
    }

    [Fact]
    public async Task GetBestStoriesAsync_LimitsResultsToRequestedCount()
    {
        // Arrange
        var storyIds = new[] { 1, 2, 3, 4, 5 };
        var stories = storyIds.Select(id => CreateTestStory(id, id * 10)).ToArray();

        var mockHandler = CreateMockHttpHandler(storyIds, stories);
        var service = CreateService(new HttpClient(mockHandler.Object));

        // Act
        var result = await service.GetBestStoriesAsync(3);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetBestStoriesAsync_UsesCachedStoryIds()
    {
        // Arrange
        var storyIds = new[] { 1, 2 };
        _memoryCache.Set("best_story_ids", storyIds);

        var stories = storyIds.Select(id => CreateTestStory(id, id * 10)).ToArray();

        var mockHandler = CreateMockHttpHandler(storyIds, stories, includeStoryIdsEndpoint: false);
        var service = CreateService(new HttpClient(mockHandler.Object));

        // Act
        var result = await service.GetBestStoriesAsync(2);

        // Assert
        result.Should().HaveCount(2);
        
        // Verify that beststories.json was never called (cache was used)
        mockHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("beststories.json")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetBestStoriesAsync_MapsFieldsCorrectly()
    {
        // Arrange
        var storyIds = new[] { 1 };
        var story = new HackerNewsStory
        {
            Id = 1,
            Title = "Test Story",
            Score = 100,
            By = "testuser",
            Time = 1609459200, // 2021-01-01 00:00:00 UTC
            Url = "http://example.com",
            Kids = new[] { 10, 20, 30 }
        };

        var mockHandler = CreateMockHttpHandler(storyIds, new[] { story });
        var service = CreateService(new HttpClient(mockHandler.Object));

        // Act
        var result = (await service.GetBestStoriesAsync(1)).First();

        // Assert
        result.Title.Should().Be("Test Story");
        result.Uri.Should().Be("http://example.com");
        result.PostedBy.Should().Be("testuser");
        result.Score.Should().Be(100);
        result.CommentCount.Should().Be(3);
        result.Time.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetBestStoriesAsync_UsesCachedStoryDetails()
    {
        // Arrange
        var storyIds = new[] { 1 };
        _memoryCache.Set("best_story_ids", storyIds);
        
        var cachedStory = new StoryResponse
        {
            Title = "Cached Story",
            Uri = "http://cached.com",
            PostedBy = "cacheduser",
            Time = "2021-01-01T00:00:00+00:00",
            Score = 999,
            CommentCount = 5
        };
        _memoryCache.Set("story_1", cachedStory);

        var mockHandler = new Mock<HttpMessageHandler>();
        var service = CreateService(new HttpClient(mockHandler.Object));

        // Act
        var result = (await service.GetBestStoriesAsync(1)).First();

        // Assert
        result.Title.Should().Be("Cached Story");
        result.Score.Should().Be(999);
        
        // Verify no HTTP calls were made (both story IDs and details were cached)
        mockHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsStaleCacheOnNetworkError()
    {
        // Arrange
        var storyIds = new[] { 1 };
        _memoryCache.Set("best_story_ids", storyIds);
        
        var staleStory = new StoryResponse
        {
            Title = "Stale Story",
            Uri = "http://stale.com",
            PostedBy = "staleuser",
            Time = "2020-01-01T00:00:00+00:00",
            Score = 500,
            CommentCount = 10
        };
        _memoryCache.Set("story_stale_1", staleStory);

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var service = CreateService(new HttpClient(mockHandler.Object));

        // Act
        var result = await service.GetBestStoriesAsync(1);

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Stale Story");
        result.First().Score.Should().Be(500);
    }

    [Fact]
    public async Task GetBestStoriesAsync_RetriesOnTransientFailure()
    {
        // Arrange
        var storyIds = new[] { 1 };
        _memoryCache.Set("best_story_ids", storyIds);
        
        var story = CreateTestStory(1, 100);
        var callCount = 0;
        
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("item/1.json")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new HttpRequestException("Transient failure");
                }
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(story)
                };
            });

        var service = CreateService(new HttpClient(mockHandler.Object));

        // Act
        var result = await service.GetBestStoriesAsync(1);

        // Assert
        result.Should().HaveCount(1);
        result.First().Score.Should().Be(100);
        callCount.Should().BeGreaterThan(1, "should have retried after failure");
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsStaleCacheOnStoryIdsFetchFailure()
    {
        // Arrange - no fresh cache, but have stale cache
        var staleIds = new[] { 1 };
        _memoryCache.Set("best_story_ids_stale", staleIds);
        
        var staleStory = new StoryResponse
        {
            Title = "Stale Story",
            Uri = "http://stale.com",
            PostedBy = "staleuser",
            Time = "2020-01-01T00:00:00+00:00",
            Score = 300,
            CommentCount = 5
        };
        _memoryCache.Set("story_stale_1", staleStory);

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("API unavailable"));

        var service = CreateService(new HttpClient(mockHandler.Object));

        // Act
        var result = await service.GetBestStoriesAsync(1);

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Stale Story");
    }
}
