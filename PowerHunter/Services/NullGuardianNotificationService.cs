using PowerHunter.Models;

namespace PowerHunter.Services;

public sealed class NullGuardianNotificationService : IGuardianNotificationService
{
    public bool CanNotify => false;

    public Task NotifyAsync(BackgroundDrainFinding finding, int additionalCount)
    {
        return Task.CompletedTask;
    }
}
