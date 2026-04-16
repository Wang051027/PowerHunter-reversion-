using SQLite;

namespace PowerHunter.Models;

/// <summary>
/// Persisted Battery Guardian event for suspicious background drain.
/// </summary>
[Table("BackgroundDrainEvents")]
public sealed class BackgroundDrainEvent
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string AppId { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public double EstimatedDrainPercent { get; set; }

    public double BackgroundUsageMinutes { get; set; }

    public double ForegroundServiceMinutes { get; set; }

    public string UsageSource { get; set; } = AppUsageSourceKind.SystemUsageStats;

    public bool IsOfficialPowerData { get; set; }

    [Indexed]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string SeverityLabel => Severity.ToUpperInvariant();

    [Ignore]
    public string SourceLabel => IsOfficialPowerData
        ? "Official system power stats"
        : "System activity stats";
}
