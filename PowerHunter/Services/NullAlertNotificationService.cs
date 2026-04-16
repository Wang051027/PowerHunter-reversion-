using PowerHunter.Models;

namespace PowerHunter.Services;

public sealed class NullAlertNotificationService : IAlertNotificationService
{
    public bool CanNotify => false;

    public Task NotifyAsync(BatteryAlert alert, AppUsageRecord triggeredApp)
    {
        return Task.CompletedTask;
    }
}
