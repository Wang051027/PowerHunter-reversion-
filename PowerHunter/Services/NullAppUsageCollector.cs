namespace PowerHunter.Services;

/// <summary>
/// Fallback for non-Android platforms. Returns no data.
/// </summary>
public sealed class NullAppUsageCollector : IAppUsageCollector
{
    public bool IsAvailable => false;

    public Task<List<RawAppUsage>> CollectAsync(DateTime since) =>
        Task.FromResult(new List<RawAppUsage>());
}
