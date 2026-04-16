using PowerHunter.Models;

namespace PowerHunter.Services;

public interface IAlertNotificationService
{
    bool CanNotify { get; }

    Task NotifyAsync(BatteryAlert alert, AppUsageRecord triggeredApp);
}
