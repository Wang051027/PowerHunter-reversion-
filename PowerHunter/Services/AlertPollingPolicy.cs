using PowerHunter.Models;

namespace PowerHunter.Services;

/// <summary>
/// Decides whether the background monitor should spend work on alert polling.
/// </summary>
public static class AlertPollingPolicy
{
    public static bool ShouldPoll(
        UserSettings settings,
        IEnumerable<BatteryAlert> alerts,
        bool isUsageCollectionAvailable)
    {
        if (!isUsageCollectionAvailable || !settings.NotificationsEnabled)
            return false;

        return alerts.Any(alert => alert.IsEnabled);
    }
}
