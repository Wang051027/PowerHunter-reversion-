using SQLite;

namespace PowerHunter.Models;

/// <summary>
/// Tracks system-reported per-app usage summaries.
/// Indexed by Date for efficient daily/weekly aggregation.
/// </summary>
[Table("AppUsageRecords")]
public sealed class AppUsageRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Application identifier (e.g., "com.tiktok.app").</summary>
    [Indexed]
    public string AppId { get; set; } = string.Empty;

    /// <summary>Human-readable app name.</summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>Normalized fallback category used by existing heuristics and legacy data.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Original system-provided app category when the OS exposes one.</summary>
    public string OriginalCategory { get; set; } = string.Empty;

    /// <summary>
    /// Percentage share of the selected system metric for the period (0-100).
    /// Prefers official per-app battery data when exposed by the OS; otherwise
    /// falls back to system usage activity share.
    /// </summary>
    public double UsagePercentage { get; set; }

    /// <summary>Foreground usage duration in minutes.</summary>
    public double UsageMinutes { get; set; }

    /// <summary>Estimated background-visible activity duration in minutes.</summary>
    public double BackgroundUsageMinutes { get; set; }

    /// <summary>Foreground service runtime in minutes, often representing persistent background work.</summary>
    public double ForegroundServiceMinutes { get; set; }

    /// <summary>System-reported power consumed in mAh when available.</summary>
    public double PowerConsumedMah { get; set; }

    /// <summary>Which system signal produced this record.</summary>
    public string UsageSource { get; set; } = AppUsageSourceKind.SystemUsageStats;

    /// <summary>Whether this row is based on official system power stats instead of usage activity.</summary>
    public bool IsOfficialPowerData { get; set; }

    /// <summary>UTC timestamp of the last successful system sync.</summary>
    public DateTime? LastSyncedAtUtc { get; set; }

    /// <summary>Date of this usage record (date only, no time component).</summary>
    [Indexed]
    public DateTime Date { get; set; } = DateTime.UtcNow.Date;
}
