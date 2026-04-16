namespace PowerHunter.Models;

/// <summary>
/// A suspicious background battery drain finding inferred from app activity.
/// </summary>
public sealed record BackgroundDrainFinding(
    string AppId,
    string AppName,
    double EstimatedDrainPercent,
    double BackgroundUsageMinutes,
    double ForegroundUsageMinutes,
    double ForegroundServiceMinutes,
    double BackgroundRatio,
    string UsageSource,
    bool IsOfficialPowerData,
    string Severity,
    string Summary
);
