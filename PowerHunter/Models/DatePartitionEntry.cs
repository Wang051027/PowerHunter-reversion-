namespace PowerHunter.Models;

/// <summary>
/// Metadata for a single yyyy-MM-dd storage partition.
/// </summary>
public sealed class DatePartitionEntry
{
    public string DateKey { get; set; } = string.Empty;

    public string RelativeDirectory { get; set; } = string.Empty;

    public string BatteryRecordsFile { get; set; } = string.Empty;

    public string AppUsageFile { get; set; } = string.Empty;

    public string ManifestFile { get; set; } = string.Empty;

    public int BatteryRecordCount { get; set; }

    public int AppUsageRecordCount { get; set; }

    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; }

    public string? ArchiveFile { get; set; }
}
