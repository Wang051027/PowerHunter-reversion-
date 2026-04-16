namespace PowerHunter.Services;

/// <summary>
/// Synchronizes system-reported app usage data.
///
/// Historical note: this service originally estimated per-app battery drain.
/// It now prefers official per-app power data when the platform exposes it and
/// otherwise falls back to usage-based activity share from the OS.
/// </summary>
public sealed class PowerEstimationService
{
    private readonly IAppUsageCollector _collector;
    private readonly PowerHunterDatabase _database;
    private readonly DatePartitionStorageService _storage;
    private DateTime? _lastCollectedAt;

    private static readonly TimeSpan MinCollectionInterval = BatteryRefreshDefaults.UsageSyncInterval;

    public PowerEstimationService(
        IAppUsageCollector collector,
        PowerHunterDatabase database,
        DatePartitionStorageService storage)
    {
        _collector = collector;
        _database = database;
        _storage = storage;
    }

    /// <summary>Whether system usage data collection is available.</summary>
    public bool IsCollectionAvailable => _collector.IsAvailable;

    /// <summary>
    /// Collects system usage data, persists it, and returns the latest records.
    /// Throttled to avoid redundant calls within the sync interval.
    /// </summary>
    public async Task<List<AppUsageRecord>> CollectAndPersistAsync(DateTime since)
    {
        // Throttle: skip if collected recently
        if (_lastCollectedAt.HasValue &&
            (DateTime.UtcNow - _lastCollectedAt.Value) < MinCollectionInterval)
        {
            return await _database.GetAppUsageAsync(since.Date);
        }

        var records = await CollectAndSummarizeAsync(since);
        if (records.Count == 0)
            return await _database.GetAppUsageAsync(since.Date);

        await _database.ReplaceAppUsageForDateAsync(since.Date, records);
        try
        {
            await _storage.PersistAppUsageRecordsAsync(since.Date, records);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PowerEstimationService] Date storage sync failed: {ex}");
        }
        _lastCollectedAt = DateTime.UtcNow;

        return records;
    }

    /// <summary>Bypasses throttle for manual refresh.</summary>
    public async Task<List<AppUsageRecord>> ForceCollectAsync(DateTime since)
    {
        _lastCollectedAt = null;
        return await CollectAndPersistAsync(since);
    }

    private async Task<List<AppUsageRecord>> CollectAndSummarizeAsync(DateTime since)
    {
        var rawData = await _collector.CollectAsync(since);
        if (rawData.Count == 0)
            return [];

        return AppUsageSummaryBuilder.Build(rawData, DateTime.UtcNow);
    }
}
