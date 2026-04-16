namespace PowerHunter.Services;

/// <summary>
/// Abstracts battery data collection across platforms.
/// Platform-specific implementations handle actual system API calls.
/// </summary>
public interface IBatteryService
{
    /// <summary>Current battery level (0.0 – 1.0).</summary>
    double CurrentLevel { get; }

    /// <summary>Current charging state.</summary>
    BatteryState CurrentState { get; }

    /// <summary>Battery power source.</summary>
    BatteryPowerSource PowerSource { get; }

    /// <summary>
    /// Starts periodic battery monitoring. Records snapshots to the database
    /// at the specified interval.
    /// </summary>
    Task StartMonitoringAsync(TimeSpan interval);

    /// <summary>Stops periodic monitoring.</summary>
    void StopMonitoring();

    /// <summary>Takes a single battery snapshot and persists it.</summary>
    Task<BatteryRecord> RecordSnapshotAsync();

    /// <summary>
    /// Retrieves app-level battery usage data from the OS.
    /// Returns empty list on platforms that don't expose this data.
    /// </summary>
    Task<List<AppUsageRecord>> GetSystemAppUsageAsync(DateTime since);

    /// <summary>Fires when battery level changes significantly.</summary>
    event EventHandler<double>? BatteryLevelChanged;
}
