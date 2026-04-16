namespace PowerHunter.Models;

/// <summary>
/// Raw per-app usage data from the OS before any summarization is applied.
/// </summary>
public sealed record RawAppUsage(
    string PackageName,
    string AppLabel,
    long ForegroundTimeMs,
    DateTime Date,
    long VisibleTimeMs = 0,
    long ForegroundServiceTimeMs = 0,
    double? ConsumedPowerMah = null,
    AppCategorySignal? CategorySignal = null
)
{
    public long BackgroundVisibleTimeMs => Math.Max(VisibleTimeMs - ForegroundTimeMs, 0);
}
