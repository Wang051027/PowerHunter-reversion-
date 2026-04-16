using SQLite;

namespace PowerHunter.Models;

/// <summary>
/// User-configured alert for battery usage thresholds.
/// </summary>
[Table("BatteryAlerts")]
public sealed class BatteryAlert
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Alert display title (e.g., "High Usage Warning").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional description of what this alert monitors.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Battery percentage threshold that triggers the alert (0-100).</summary>
    public double ThresholdPercent { get; set; }

    /// <summary>Whether this alert is currently active.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>When the alert was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the alert last fired (null if never).</summary>
    public DateTime? LastTriggeredAt { get; set; }
}
