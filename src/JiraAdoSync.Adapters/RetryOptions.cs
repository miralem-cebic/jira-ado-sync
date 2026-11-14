namespace JiraAdoSync.Adapters;

/// <summary>Limits of the retry policy in ADR 0005.</summary>
public sealed record RetryOptions
{
    /// <summary>Attempts per request, including the first one.</summary>
    public int MaxAttempts { get; init; } = 4;

    /// <summary>Delay of the first retry before jitter. Each further retry doubles it.</summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Longest single wait. A longer <c>Retry-After</c> is not waited for.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);
}
