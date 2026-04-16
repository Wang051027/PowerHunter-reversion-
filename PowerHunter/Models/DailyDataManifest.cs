namespace PowerHunter.Models;

/// <summary>
/// Per-day manifest stored alongside the date-partitioned files.
/// </summary>
public sealed class DailyDataManifest
{
    public string DateKey { get; set; } = string.Empty;

    public string BatteryRecordsFile { get; set; } = string.Empty;

    public string AppUsageFile { get; set; } = string.Empty;

    public int BatteryRecordCount { get; set; }

    public int AppUsageRecordCount { get; set; }

    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; }

    public string? ArchiveFile { get; set; }
}
