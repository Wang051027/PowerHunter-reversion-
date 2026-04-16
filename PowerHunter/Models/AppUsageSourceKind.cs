namespace PowerHunter.Models;

/// <summary>
/// Declares which system signal was used to build app-level usage records.
/// </summary>
public static class AppUsageSourceKind
{
    public const string OfficialBatteryStats = "official-battery-stats";
    public const string SystemUsageStats = "system-usage-stats";

    public static bool IsOfficial(string? source)
        => string.Equals(source, OfficialBatteryStats, StringComparison.OrdinalIgnoreCase);
}
