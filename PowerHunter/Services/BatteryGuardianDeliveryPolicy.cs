namespace PowerHunter.Services;

public static class BatteryGuardianDeliveryPolicy
{
    public static BatteryGuardianDeliveryMode Decide(
        bool isAppInForeground,
        bool notificationsEnabled,
        bool canSendLocalNotification)
    {
        if (isAppInForeground)
            return BatteryGuardianDeliveryMode.InAppDialog;

        if (notificationsEnabled && canSendLocalNotification)
            return BatteryGuardianDeliveryMode.LocalNotification;

        return BatteryGuardianDeliveryMode.None;
    }
}
