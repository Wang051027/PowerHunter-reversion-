using SQLite;

namespace PowerHunter.Models;

/// <summary>
/// Records battery level snapshots over time.
/// Indexed by RecordedAt for efficient date-range queries.
/// </summary>
[Table("BatteryRecords")]
public sealed class BatteryRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Battery level percentage (0-100).</summary>
    public double BatteryLevel { get; set; }

    /// <summary>Number of active usage sessions at the time of recording.</summary>
    public int SessionCount { get; set; }

    /// <summary>Battery charging state (Charging, Discharging, Full, NotCharging, Unknown).</summary>
    public string ChargingState { get; set; } = "Unknown";

    /// <summary>UTC timestamp of when this record was taken.</summary>
    [Indexed]
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
