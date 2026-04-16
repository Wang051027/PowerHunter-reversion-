using PowerHunter.Models;

namespace PowerHunter.Services;

public interface IGuardianNotificationService
{
    bool CanNotify { get; }

    Task NotifyAsync(BackgroundDrainFinding finding, int additionalCount);
}
