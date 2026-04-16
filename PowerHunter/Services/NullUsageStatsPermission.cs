namespace PowerHunter.Services;

/// <summary>
/// Fallback for non-Android platforms. Permission not applicable.
/// </summary>
public sealed class NullUsageStatsPermission : IUsageStatsPermission
{
    public bool IsSupported => false;
    public bool IsGranted => false;
    public Task RequestAsync() => Task.CompletedTask;
}
