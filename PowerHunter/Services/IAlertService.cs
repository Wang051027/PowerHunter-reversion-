using PowerHunter.Models;

namespace PowerHunter.Services;

/// <summary>
/// Manages battery usage alerts and local notifications.
/// </summary>
public interface IAlertService
{
    /// <summary>Creates a new alert and persists it.</summary>
    Task<BatteryAlert> CreateAlertAsync(string title, string description, double thresholdPercent);

    /// <summary>Toggles an alert on/off.</summary>
    Task ToggleAlertAsync(int alertId, bool isEnabled);

    /// <summary>Deletes an alert.</summary>
    Task DeleteAlertAsync(int alertId);

    /// <summary>Returns all configured alerts.</summary>
    Task<List<BatteryAlert>> GetAlertsAsync();

    /// <summary>
    /// Evaluates all enabled alerts against the latest per-app battery usage share.
    /// Fires system notifications for any that trigger.
    /// </summary>
    Task EvaluateAlertsAsync(IEnumerable<AppUsageRecord> usageRecords);
}
