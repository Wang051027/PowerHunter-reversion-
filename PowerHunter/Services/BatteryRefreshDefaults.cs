namespace PowerHunter.Services;

/// <summary>
/// Centralized refresh intervals so battery snapshots, usage sync, and UI polling
/// all follow the same periodic update strategy.
/// </summary>
public static class BatteryRefreshDefaults
{
    public static readonly TimeSpan AlertCheckInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan InAppSnapshotInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan BackgroundSnapshotInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan NightPowerSavingSnapshotInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan UiRefreshInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan UsageSyncInterval = TimeSpan.FromSeconds(30);
}
