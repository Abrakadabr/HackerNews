using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using HackerNews.Api.Configuration;
using HackerNews.Api.Models;

namespace HackerNews.Api.Services;

/// <summary>
/// Service for interacting with the Hacker News API.
/// Implements caching and circuit breaker pattern for resilience.
/// </summary>
public class HackerNewsService : IHackerNewsService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HackerNewsService> _logger;
    private readonly HackerNewsApiSettings _settings;
    private readonly CacheSettings _cacheSettings;
    private readonly ResiliencePipeline _resiliencePipeline;

    private const string BestStoriesIdsUrl = "beststories.json";
    private const string StoryDetailUrlTemplate = "item/{0}.json";
    private const string StoryIdsCacheKey = "best_story_ids";
    private const string StoryIdsStaleCacheKey = "best_story_ids_stale";
    private const string StoryDetailCacheKeyPrefix = "story_";
    private const string StoryDetailStaleCacheKeyPrefix = "story_stale_";

    public HackerNewsService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<HackerNewsService> logger,
        IOptions<HackerNewsApiSettings> settings,
        IOptions<CacheSettings> cacheSettings,
        IOptions<ResilienceSettings> resilienceSettings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _cacheSettings = cacheSettings?.Value ?? throw new ArgumentNullException(nameof(cacheSettings));
        
        var resilienceConfig = resilienceSettings?.Value ?? throw new ArgumentNullException(nameof(resilienceSettings));

        ConfigureHttpClientBaseAddress();
        _resiliencePipeline = ResiliencePipelineFactory.Create(resilienceConfig, _logger);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StoryResponse>> GetBestStoriesAsync(int count, CancellationToken cancellationToken = default)
    {
        try
        {
            var storyIds = await GetBestStoryIdsAsync(cancellationToken);
            var topStoryIds = storyIds.Take(count).ToList();

            _logger.LogDebug("Fetching details for {Count} stories", topStoryIds.Count);

            var stories = await FetchStoriesInParallelAsync(topStoryIds, cancellationToken);

            return stories.OrderByDescending(s => s.Score).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving best stories");
            throw;
        }
    }

    private void ConfigureHttpClientBaseAddress()
    {
        var baseUrl = _settings.BaseUrl;
        if (!string.IsNullOrWhiteSpace(baseUrl) && !baseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            baseUrl += "/";
        }
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    private async Task<int[]> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        if (TryGetFromCache<int[]>(StoryIdsCacheKey, out var cachedIds))
        {
            _logger.LogDebug("Retrieved {Count} story IDs from cache", cachedIds!.Length);
            return cachedIds;
        }

        _logger.LogInformation("Cache miss for story IDs. Fetching from API...");

        try
        {
            var storyIds = await FetchStoryIdsFromApiAsync(cancellationToken);
            CacheStoryIds(storyIds);
            return storyIds;
        }
        catch (BrokenCircuitException)
        {
            return GetStaleStoryIdsOrThrow("circuit breaker open");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error fetching story IDs, attempting stale cache fallback");
            return GetStaleStoryIdsOrThrow("network error");
        }
    }

    private async Task<int[]> FetchStoryIdsFromApiAsync(CancellationToken cancellationToken)
    {
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var response = await _httpClient.GetFromJsonAsync<int[]>(BestStoriesIdsUrl, token);
            return response ?? Array.Empty<int>();
        }, cancellationToken);
    }

    private void CacheStoryIds(int[] storyIds)
    {
        SetCache(StoryIdsCacheKey, storyIds, TimeSpan.FromSeconds(_cacheSettings.StoryIdsExpirationSeconds));
        SetCache(StoryIdsStaleCacheKey, storyIds, TimeSpan.FromSeconds(_cacheSettings.StaleCacheExpirationSeconds));

        _logger.LogDebug(
            "Cached {Count} story IDs (fresh: {FreshSeconds}s, stale: {StaleSeconds}s)",
            storyIds.Length,
            _cacheSettings.StoryIdsExpirationSeconds,
            _cacheSettings.StaleCacheExpirationSeconds);
    }

    private int[] GetStaleStoryIdsOrThrow(string reason)
    {
        _logger.LogWarning("Attempting stale cache fallback for story IDs due to {Reason}", reason);

        if (TryGetFromCache<int[]>(StoryIdsStaleCacheKey, out var staleIds))
        {
            _logger.LogInformation("Returning {Count} stale cached story IDs due to {Reason}", staleIds!.Length, reason);
            return staleIds;
        }

        _logger.LogError("No stale cache available for story IDs and {Reason}", reason);
        throw new InvalidOperationException($"Unable to fetch story IDs: {reason} and no stale cache available");
    }

    private async Task<List<StoryResponse>> FetchStoriesInParallelAsync(
        List<int> storyIds,
        CancellationToken cancellationToken)
    {
        var results = new ConcurrentBag<StoryResponse>();

        await Parallel.ForEachAsync(
            storyIds,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _settings.MaxConcurrentRequests,
                CancellationToken = cancellationToken
            },
            async (storyId, ct) =>
            {
                var story = await GetStoryDetailsAsync(storyId, ct);
                if (story != null)
                {
                    results.Add(story);
                }
            });

        return results.ToList();
    }

    private async Task<StoryResponse?> GetStoryDetailsAsync(int storyId, CancellationToken cancellationToken)
    {
        var cacheKey = $"{StoryDetailCacheKeyPrefix}{storyId}";
        var staleCacheKey = $"{StoryDetailStaleCacheKeyPrefix}{storyId}";

        if (TryGetFromCache<StoryResponse>(cacheKey, out var cachedStory))
        {
            _logger.LogDebug("Retrieved story {StoryId} from cache", storyId);
            return cachedStory;
        }

        try
        {
            return await FetchAndCacheStoryAsync(storyId, cacheKey, staleCacheKey, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw; // Respect cancellation requests
        }
        catch (BrokenCircuitException)
        {
            return GetStaleStoryOrNull(staleCacheKey, storyId, "circuit breaker open");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error fetching story {StoryId}", storyId);
            return GetStaleStoryOrNull(staleCacheKey, storyId, "network error");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize story {StoryId}", storyId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching story {StoryId}", storyId);
            return null;
        }
    }

    private async Task<StoryResponse?> FetchAndCacheStoryAsync(
        int storyId,
        string cacheKey,
        string staleCacheKey,
        CancellationToken cancellationToken)
    {
        var story = await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var url = string.Format(StoryDetailUrlTemplate, storyId);
            return await _httpClient.GetFromJsonAsync<HackerNewsStory>(url, token);
        }, cancellationToken);

        if (story == null)
        {
            _logger.LogWarning("Story {StoryId} returned null from API", storyId);
            return null;
        }

        var storyResponse = MapToStoryResponse(story);
        CacheStoryDetails(cacheKey, staleCacheKey, storyResponse, storyId);
        return storyResponse;
    }

    private void CacheStoryDetails(string cacheKey, string staleCacheKey, StoryResponse storyResponse, int storyId)
    {
        SetCache(cacheKey, storyResponse, TimeSpan.FromSeconds(_cacheSettings.StoryDetailsExpirationSeconds));
        SetCache(staleCacheKey, storyResponse, TimeSpan.FromSeconds(_cacheSettings.StaleCacheExpirationSeconds));

        _logger.LogDebug(
            "Cached story {StoryId} (fresh: {FreshSeconds}s, stale: {StaleSeconds}s)",
            storyId,
            _cacheSettings.StoryDetailsExpirationSeconds,
            _cacheSettings.StaleCacheExpirationSeconds);
    }

    private StoryResponse? GetStaleStoryOrNull(string staleCacheKey, int storyId, string reason)
    {
        _logger.LogWarning("Attempting stale cache fallback for story {StoryId} due to {Reason}", storyId, reason);

        if (TryGetFromCache<StoryResponse>(staleCacheKey, out var staleStory))
        {
            _logger.LogInformation("Returning stale cached data for story {StoryId} due to {Reason}", storyId, reason);
            return staleStory;
        }

        _logger.LogWarning("No stale cache available for story {StoryId}", storyId);
        return null;
    }

    private bool TryGetFromCache<T>(string key, out T? value) where T : class
    {
        if (_cache.TryGetValue(key, out T? cached) && cached != null)
        {
            value = cached;
            return true;
        }
        value = default;
        return false;
    }

    private void SetCache<T>(string key, T value, TimeSpan expiration)
    {
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(expiration);
        _cache.Set(key, value, options);
    }

    private static StoryResponse MapToStoryResponse(HackerNewsStory story)
    {
        return new StoryResponse
        {
            Title = story.Title ?? string.Empty,
            Uri = story.Url ?? string.Empty,
            PostedBy = story.By ?? string.Empty,
            Time = DateTimeOffset.FromUnixTimeSeconds(story.Time).ToString("yyyy-MM-ddTHH:mm:ssK"),
            Score = story.Score,
            CommentCount = story.Kids?.Length ?? 0
        };
    }
}
