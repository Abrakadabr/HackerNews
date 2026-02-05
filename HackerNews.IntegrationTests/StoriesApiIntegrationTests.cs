using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using HackerNews.Api.Models;

namespace HackerNews.IntegrationTests;

/// <summary>
/// Integration tests for the Stories API endpoint.
/// These tests verify the API works correctly with the real Hacker News API.
/// </summary>
public class StoriesApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public StoriesApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetBestStories_WithValidCount_ReturnsSuccessStatusCode()
    {
        // Arrange
        var count = 5;

        // Act
        var response = await _client.GetAsync($"/api/stories/best?count={count}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBestStories_WithValidCount_ReturnsCorrectNumberOfStories()
    {
        // Arrange
        var count = 3;

        // Act
        var stories = await _client.GetFromJsonAsync<List<StoryResponse>>($"/api/stories/best?count={count}");

        // Assert
        stories.Should().NotBeNull();
        stories.Should().HaveCount(count);
    }

    [Fact]
    public async Task GetBestStories_ReturnsStoriesSortedByScoreDescending()
    {
        // Arrange
        var count = 10;

        // Act
        var stories = await _client.GetFromJsonAsync<List<StoryResponse>>($"/api/stories/best?count={count}");

        // Assert
        stories.Should().NotBeNull();
        stories.Should().HaveCount(count);
        stories.Should().BeInDescendingOrder(s => s.Score);
    }

    [Fact]
    public async Task GetBestStories_ReturnsStoriesWithAllRequiredFields()
    {
        // Arrange
        var count = 2;

        // Act
        var stories = await _client.GetFromJsonAsync<List<StoryResponse>>($"/api/stories/best?count={count}");

        // Assert
        stories.Should().NotBeNull();
        stories.Should().HaveCount(count);
        
        foreach (var story in stories!)
        {
            story.Title.Should().NotBeNullOrEmpty();
            story.PostedBy.Should().NotBeNullOrEmpty();
            story.Time.Should().NotBeNullOrEmpty();
            story.Score.Should().BeGreaterThan(0);
            // Uri and CommentCount can be empty/zero for some stories
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public async Task GetBestStories_WithInvalidCount_ReturnsBadRequest(int invalidCount)
    {
        // Act
        var response = await _client.GetAsync($"/api/stories/best?count={invalidCount}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBestStories_WithDefaultCount_ReturnsDefaultNumberOfStories()
    {
        // Act
        var stories = await _client.GetFromJsonAsync<List<StoryResponse>>("/api/stories/best");

        // Assert
        stories.Should().NotBeNull();
        stories.Should().HaveCount(10); // Default count
    }
}
