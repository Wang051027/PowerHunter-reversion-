using PowerHunter.Models;

namespace PowerHunter.Services;

/// <summary>
/// Decides whether a saved battery alert should emit a system notification.
/// </summary>
public static class AlertTriggerPolicy
{
    // Keep alert repeats aligned with the 30-second polling cadence so a sustained
    // threshold breach can notify again on the next scheduled check.
    private static readonly TimeSpan TriggerCooldown = BatteryRefreshDefaults.AlertCheckInterval;

    public static AppUsageRecord? FindTriggeredApp(
        BatteryAlert alert,
        IEnumerable<AppUsageRecord> usageRecords,
        bool smartAlertsEnabled,
        bool notificationsEnabled,
        bool canSendLocalNotification,
        DateTime nowUtc)
    {
        if (!smartAlertsEnabled || !notificationsEnabled || !canSendLocalNotification)
            return null;

        if (!alert.IsEnabled)
            return null;

        if (alert.LastTriggeredAt.HasValue &&
            (nowUtc - alert.LastTriggeredAt.Value) < TriggerCooldown)
        {
            return null;
        }

        return usageRecords
            .Where(record => record.UsagePercentage >= alert.ThresholdPercent)
            .OrderByDescending(record => record.UsagePercentage)
            .FirstOrDefault();
    }
}
