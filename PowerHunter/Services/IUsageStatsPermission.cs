namespace PowerHunter.Services;

/// <summary>
/// Platform abstraction for the PACKAGE_USAGE_STATS special permission.
/// Android: checks AppOpsManager and opens Settings intent.
/// Other platforms: returns not supported (NullUsageStatsPermission).
/// </summary>
public interface IUsageStatsPermission
{
    /// <summary>Whether this platform supports usage stats at all.</summary>
    bool IsSupported { get; }

    /// <summary>Whether the permission is currently granted.</summary>
    bool IsGranted { get; }

    /// <summary>Opens the system settings screen where the user can grant the permission.</summary>
    Task RequestAsync();
}
