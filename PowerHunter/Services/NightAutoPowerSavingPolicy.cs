using PowerHunter.Models;

namespace PowerHunter.Services;

/// <summary>
/// Determines when the app should switch into a lower-frequency monitoring mode overnight.
/// </summary>
public static class NightAutoPowerSavingPolicy
{
    public const int DefaultStartHour = 22;
    public const int DefaultEndHour = 7;

    public static bool IsActive(UserSettings settings, DateTime localNow)
    {
        if (!settings.NightAutoPowerSavingEnabled)
            return false;

        return IsWithinWindow(localNow, DefaultStartHour, DefaultEndHour);
    }

    public static TimeSpan ResolveMonitoringInterval(
        UserSettings settings,
        TimeSpan defaultInterval,
        TimeSpan powerSavingInterval,
        DateTime localNow)
    {
        if (!IsActive(settings, localNow))
            return defaultInterval;

        return defaultInterval >= powerSavingInterval
            ? defaultInterval
            : powerSavingInterval;
    }

    internal static bool IsWithinWindow(DateTime localNow, int startHourInclusive, int endHourExclusive)
    {
        var hour = localNow.Hour;

        if (startHourInclusive == endHourExclusive)
            return true;

        if (startHourInclusive < endHourExclusive)
            return hour >= startHourInclusive && hour < endHourExclusive;

        return hour >= startHourInclusive || hour < endHourExclusive;
    }
}
