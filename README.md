#  Hacker News API

A RESTful API built with ASP.NET Core 9.0 that retrieves the best stories from the Hacker News API, with caching, circuit breaker pattern, and rate limiting for optimal performance and resilience.

## Overview

This API provides an endpoint to fetch the top N "best stories" from Hacker News, sorted by score in descending order. The implementation includes production-ready features such as caching, resilience patterns, rate limiting, and comprehensive error handling.

## Features

- **RESTful API**: Clean endpoint design following REST principles
- **Caching**: In-memory caching to minimize API calls to Hacker News
- **Circuit Breaker**: Polly-based circuit breaker pattern for resilience
- **Rate Limiting**: Configurable rate limiting to protect the API
- **Parallel Processing**: Efficient concurrent fetching of story details
- **Swagger/OpenAPI**: Interactive API documentation
- **Comprehensive Logging**: Built-in structured logging
- **Unit Tests**: Full test coverage with xUnit, Moq, and FluentAssertions
- **Input Validation**: Robust parameter validation with clear error messages

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- An IDE (Visual Studio, Visual Studio Code, or JetBrains Rider)

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd HackerNews
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Run the Application

```bash
cd HackerNews.Api
dotnet run
```

The API will start at:
- HTTP: `http://localhost:5079`
- HTTPS: `https://localhost:7026`

### 4. Access Swagger UI

Open your browser and navigate to:
```
http://localhost:5079
```

Or for HTTPS:
```
https://localhost:7026
```

The Swagger UI provides interactive documentation where you can test the API directly.

## API Endpoints

### GET /api/stories/best

Retrieves the best stories from Hacker News.

**Query Parameters:**
- `count` (optional): Number of stories to retrieve (1-500, default: 10)

**Example Requests:**

```bash
# Get 10 best stories (default)
curl http://localhost:5079/api/stories/best

# Get 20 best stories
curl http://localhost:5079/api/stories/best?count=20

# Get 100 best stories
curl http://localhost:5079/api/stories/best?count=100
```

**Example Response:**

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  },
  {
    "title": "Another great story",
    "uri": "https://example.com/story",
    "postedBy": "user123",
    "time": "2019-10-12T14:30:00+00:00",
    "score": 1500,
    "commentCount": 320
  }
]
```

**Response Codes:**
- `200 OK`: Stories retrieved successfully
- `400 Bad Request`: Invalid count parameter
- `429 Too Many Requests`: Rate limit exceeded
- `500 Internal Server Error`: Server error occurred

## Configuration

All configuration settings are in `appsettings.json`:

### Hacker News API Settings

```json
{
  "HackerNewsApi": {
    "BaseUrl": "https://hacker-news.firebaseio.com/v0/",
    "MaxConcurrentRequests": 10,
    "RequestTimeoutSeconds": 30
  }
}
```

- `MaxConcurrentRequests`: Maximum parallel requests when fetching story details
- `RequestTimeoutSeconds`: Timeout for each HTTP request

### Cache Settings

```json
{
  "CacheSettings": {
    "StoryIdsExpirationSeconds": 300,
    "StoryDetailsExpirationSeconds": 600,
    "StaleCacheExpirationSeconds": 3600
  }
}
```

- `StoryIdsExpirationSeconds`: How long (in seconds) to cache the list of best story IDs (fresh cache)
- `StoryDetailsExpirationSeconds`: How long (in seconds) to cache individual story details (fresh cache)
- `StaleCacheExpirationSeconds`: How long (in seconds) to keep stale cache for fallback during outages

### Resilience Settings

```json
{
  "Resilience": {
    "TimeoutSeconds": 30,
    "CircuitBreakerFailureRatio": 0.5,
    "CircuitBreakerSamplingDurationSeconds": 30,
    "CircuitBreakerMinimumThroughput": 3,
    "CircuitBreakerBreakDurationSeconds": 30,
    "RetryMaxAttempts": 2,
    "RetryDelayMilliseconds": 500
  }
}
```

- `TimeoutSeconds`: Maximum time to wait for HTTP requests
- `CircuitBreakerFailureRatio`: Failure percentage threshold to open the circuit (50%)
- `CircuitBreakerSamplingDurationSeconds`: Time window for measuring failures
- `CircuitBreakerMinimumThroughput`: Minimum requests before circuit can open
- `CircuitBreakerBreakDurationSeconds`: How long circuit stays open before recovery attempt
- `RetryMaxAttempts`: Number of retry attempts for transient failures
- `RetryDelayMilliseconds`: Initial delay between retries (uses exponential backoff)

### Rate Limiting

```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 100
      },
      {
        "Endpoint": "*",
        "Period": "1h",
        "Limit": 1000
      }
    ]
  }
}
```

- Limits: 100 requests per minute, 1000 requests per hour per IP
- Can be adjusted based on requirements

## Running Tests

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run tests with coverage
dotnet test /p:CollectCoverage=true
```

The test suite includes:
- **Unit Tests** (11 total):
  - **Service Tests**: 4 tests covering HackerNewsService functionality
  - **Controller Tests**: 7 tests covering StoriesController behavior (using Theory for boundary tests)
- **Integration Tests** (6 tests): Full end-to-end API testing with real HTTP calls

**Recent Improvements:**
- Eliminated ~140 lines of duplicate test code
- Converted repetitive tests to xUnit Theory patterns
- Added helper methods for cleaner, more maintainable tests
- All 17 tests passing with improved readability

## Project Structure

```
HackerNews/
├── HackerNews.Api/
│   ├── Configuration/           # Configuration classes
│   │   ├── CacheSettings.cs
│   │   └── HackerNewsApiSettings.cs
│   ├── Controllers/             # API controllers
│   │   └── StoriesController.cs
│   ├── Models/                  # Data models
│   │   ├── HackerNewsStory.cs
│   │   └── StoryResponse.cs
│   ├── Services/                # Business logic
│   │   ├── IHackerNewsService.cs
│   │   └── HackerNewsService.cs
│   ├── appsettings.json
│   └── Program.cs
├── HackerNews.Tests/         # Unit tests
│   ├── Controllers/
│   │   └── StoriesControllerTests.cs
│   └── Services/
│       └── HackerNewsServiceTests.cs
├── HackerNews.IntegrationTests/  # Integration tests
│   └── StoriesApiIntegrationTests.cs
└── README.md
```

## Architecture & Design Decisions

### Caching Strategy

The implementation uses **IMemoryCache** with a **dual TTL strategy**:

**Fresh Cache (Primary)**:
- Story IDs are cached for 5 minutes
- Individual story details are cached for 10 minutes
- Reduces load on Hacker News API
- Improves response times for subsequent requests

**Stale Cache (Fallback)**:
- All data is also stored with a longer TTL (60 minutes by default)
- Used as fallback when the circuit breaker is open or network errors occur
- Ensures availability during external service outages
- Prevents cascading failures while maintaining user experience

```
Request Flow:
+-----------+     +-------------+     +-----------------+     +------------+
| Request   |---->| Fresh Cache |---->| Resilience      |---->| Hacker News|
+-----------+     +-------------+     | Pipeline        |     | API        |
                       |              +-----------------+     +------------+
                   Cache hit                 |
                       |            On failure/circuit open
                       v                      |
                  Return data                 v
                                     +-------------+
                                     | Stale Cache |
                                     +-------------+
```

**Note**: For distributed/production environments with multiple instances, consider using **Redis** (IDistributedCache) for cache synchronization across servers.

### Resilience Pipeline

The resilience pipeline is implemented using **Polly v8** with the following order:

```
Timeout (outer) → Retry → Circuit Breaker (inner)
```

This order ensures:
1. **Timeout**: Slow requests are terminated quickly to free up resources
2. **Retry**: Transient failures are automatically retried with exponential backoff
3. **Circuit Breaker**: Persistent failures trigger the circuit breaker to protect the system

#### Retry Policy
- **Max Attempts**: 2 retries (3 total attempts)
- **Backoff**: Exponential (500ms, 1000ms)
- **Handles**: `HttpRequestException`, `TimeoutRejectedException`
- **Benefit**: Recovers from transient network issues without burdening the circuit breaker

#### Circuit Breaker Pattern
- **Failure Ratio Threshold**: 50%
- **Sampling Duration**: 30 seconds
- **Minimum Throughput**: 3 requests (before circuit can open)
- **Break Duration**: 30 seconds

This prevents cascade failures when the Hacker News API is experiencing issues.

### Parallel Processing

Story details are fetched in parallel using `Parallel.ForEachAsync` (.NET 6+):
- Configurable `MaxDegreeOfParallelism` (default: 10)
- Built-in throttling via `ParallelOptions`
- Thread-safe result collection with `ConcurrentBag<T>`
- Efficient handling of large result sets

### Rate Limiting

Uses **AspNetCoreRateLimit** to:
- Prevent API abuse
- Ensure fair usage
- Protect backend resources

## Assumptions

1. **Story Order**: The Hacker News `/beststories.json` endpoint returns story IDs already ordered by "best" criteria
2. **Story Availability**: Not all story IDs may return valid data (handled gracefully)
3. **Score Sorting**: Stories are sorted by score in descending order after fetching
4. **Count Limit**: Maximum count is capped at 500 for performance reasons
5. **Cache Duration**: Cache times are balanced between freshness and performance
6. **Single Instance**: IMemoryCache is suitable for single-instance deployments

## Production Enhancements

For a production environment, consider these enhancements:

### 1. Distributed Caching
Replace IMemoryCache with Redis:
```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration["Redis:ConnectionString"];
});
```

### 2. Centralized Logging
Integrate with Application Insights, ELK Stack, or Seq:
```csharp
services.AddApplicationInsightsTelemetry();
```

### 3. Health Checks
Add health check endpoints:
```csharp
services.AddHealthChecks()
    .AddUrlGroup(new Uri("https://hacker-news.firebaseio.com/v0/beststories.json"), "HackerNews API");
```

### 4. API Versioning
Support multiple API versions:
```csharp
services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
});
```

### 5. Metrics & Monitoring
Add Prometheus metrics:
```csharp
services.AddPrometheusMetrics();
```

### 6. Background Jobs
Use Hangfire or Quartz.NET for periodic cache warming

### 7. API Gateway
Deploy behind Azure API Management or AWS API Gateway for:
- Advanced rate limiting
- API key management
- Analytics
- Transformation policies

### 8. Container Deployment
Dockerize the application:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HackerNews.Api.dll"]
```

## Troubleshooting

### Issue: API returns 429 (Too Many Requests)
**Solution**: Wait for the rate limit window to reset (1 minute) or adjust rate limiting configuration

### Issue: Slow response times
**Solution**: 
- Check if cache is working (look for cache hit logs)
- Increase `MaxConcurrentRequests` in configuration
- Verify network connectivity to Hacker News API

### Issue: Circuit breaker is open
**Solution**: 
- Check Hacker News API status
- Wait for the break duration (30 seconds)
- Review logs for underlying errors

## License

This project is provided as a coding test solution for .

## Contact

For questions or feedback about this implementation, please contact the development team.

## Acknowledgments

- [Hacker News API](https://github.com/HackerNews/API) - For providing the data
- [Polly](https://github.com/App-vNext/Polly) - For resilience patterns
- [AspNetCoreRateLimit](https://github.com/stefanprodan/AspNetCoreRateLimit) - For rate limiting
