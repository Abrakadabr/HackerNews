namespace HackerNews.Api.Configuration;

/// <summary>
/// Configuration settings for HTTP resilience policies.
/// </summary>
public class ResilienceSettings
{
    /// <summary>
    /// The section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Resilience";

    /// <summary>
    /// Timeout in seconds for outbound HTTP requests.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Failure ratio threshold to open the circuit breaker.
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Sampling duration in seconds for circuit breaker evaluation.
    /// </summary>
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum throughput required before circuit breaker can open.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 3;

    /// <summary>
    /// Break duration in seconds when the circuit opens.
    /// </summary>
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts for transient failures.
    /// </summary>
    public int RetryMaxAttempts { get; set; } = 2;

    /// <summary>
    /// Initial delay in milliseconds between retry attempts.
    /// Uses exponential backoff, so subsequent delays will be longer.
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 500;
}
