namespace PowerHunter.Models;

/// <summary>
/// Result of archiving historical records out of the online SQLite store.
/// </summary>
public sealed class ArchiveMaintenanceResult
{
    public int ArchivedDayCount { get; set; }

    public int BatteryRecordCount { get; set; }

    public int AppUsageRecordCount { get; set; }

    public long DatabaseBytesBefore { get; set; }

    public long DatabaseBytesAfter { get; set; }

    public string ArchiveRoot { get; set; } = string.Empty;

    public bool HadWork => ArchivedDayCount > 0;

    public long ReleasedDatabaseBytes => Math.Max(DatabaseBytesBefore - DatabaseBytesAfter, 0);
}
