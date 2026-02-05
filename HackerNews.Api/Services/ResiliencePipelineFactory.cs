using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using HackerNews.Api.Configuration;

namespace HackerNews.Api.Services;

/// <summary>
/// Factory for creating resilience pipelines with standardized configuration.
/// Extracts complex pipeline setup from service constructors for better testability and readability.
/// </summary>
public static class ResiliencePipelineFactory
{
    /// <summary>
    /// Creates a resilience pipeline with timeout, retry, and circuit breaker strategies.
    /// </summary>
    /// <param name="settings">Resilience configuration settings.</param>
    /// <param name="logger">Logger for resilience events.</param>
    /// <returns>Configured resilience pipeline.</returns>
    /// <remarks>
    /// Pipeline order: Timeout (outer) → Retry → Circuit Breaker (inner)
    /// This ensures:
    /// - Slow requests are terminated by timeout
    /// - Transient failures are retried before counting towards circuit breaker
    /// - Persistent failures trigger circuit breaker to protect the system
    /// </remarks>
    public static ResiliencePipeline Create(ResilienceSettings settings, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        return new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(settings.TimeoutSeconds))
            .AddRetry(CreateRetryOptions(settings, logger))
            .AddCircuitBreaker(CreateCircuitBreakerOptions(settings, logger))
            .Build();
    }

    private static RetryStrategyOptions CreateRetryOptions(ResilienceSettings settings, ILogger logger)
    {
        return new RetryStrategyOptions
        {
            MaxRetryAttempts = settings.RetryMaxAttempts,
            Delay = TimeSpan.FromMilliseconds(settings.RetryDelayMilliseconds),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>(),
            OnRetry = args =>
            {
                logger.LogWarning(
                    "Retry attempt {AttemptNumber} after {Delay}ms due to: {ExceptionMessage}",
                    args.AttemptNumber,
                    args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Exception?.Message ?? "Unknown error");
                return default;
            }
        };
    }

    private static CircuitBreakerStrategyOptions CreateCircuitBreakerOptions(ResilienceSettings settings, ILogger logger)
    {
        return new CircuitBreakerStrategyOptions
        {
            FailureRatio = settings.CircuitBreakerFailureRatio,
            SamplingDuration = TimeSpan.FromSeconds(settings.CircuitBreakerSamplingDurationSeconds),
            MinimumThroughput = settings.CircuitBreakerMinimumThroughput,
            BreakDuration = TimeSpan.FromSeconds(settings.CircuitBreakerBreakDurationSeconds),
            OnOpened = args =>
            {
                logger.LogWarning(
                    "Circuit breaker opened due to failures. Break duration: {BreakDuration}s",
                    args.BreakDuration.TotalSeconds);
                return default;
            },
            OnClosed = _ =>
            {
                logger.LogInformation("Circuit breaker closed. Service is healthy again.");
                return default;
            }
        };
    }
}
