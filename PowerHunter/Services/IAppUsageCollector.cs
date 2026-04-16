namespace PowerHunter.Services;

/// <summary>
/// Platform abstraction for collecting per-app usage data.
/// Android: queries UsageStatsManager.
/// Other platforms: returns empty (NullAppUsageCollector).
/// </summary>
public interface IAppUsageCollector
{
    /// <summary>Whether system app activity collection is available on this platform and permission is granted.</summary>
    bool IsAvailable { get; }

    /// <summary>Collects raw per-app system activity and optional power data since the given time.</summary>
    Task<List<RawAppUsage>> CollectAsync(DateTime since);
}
