using PowerHunter.Models;

namespace PowerHunter.Services;

/// <summary>
/// Coordinates Battery Guardian background drain detection and event persistence.
/// </summary>
public sealed class BatteryGuardianService
{
    private static readonly TimeSpan FindingCooldown = TimeSpan.FromHours(2);
    private readonly PowerHunterDatabase _database;
    private readonly IAppVisibilityService _appVisibility;
    private readonly IGuardianNotificationService _notificationService;

    public BatteryGuardianService(
        PowerHunterDatabase database,
        IAppVisibilityService appVisibility,
        IGuardianNotificationService notificationService)
    {
        _database = database;
        _appVisibility = appVisibility;
        _notificationService = notificationService;
    }

    public async Task<List<BackgroundDrainFinding>> EvaluateAsync(IEnumerable<AppUsageRecord> records, DateTime observedSince)
    {
        var settings = await _database.GetSettingsAsync();
        if (!settings.GuardianEnabled)
            return [];

        if (!await HasEnoughBatterySignalAsync(observedSince))
            return [];

        var findings = BackgroundDrainAnalyzer.Analyze(records);
        if (findings.Count == 0)
            return [];

        var newFindings = new List<BackgroundDrainFinding>();

        foreach (var finding in findings)
        {
            var latest = await _database.GetLatestBackgroundDrainEventAsync(finding.AppId);
            if (latest is not null &&
                (DateTime.UtcNow - latest.DetectedAt) < FindingCooldown)
            {
                continue;
            }

            var newEvent = new BackgroundDrainEvent
            {
                AppId = finding.AppId,
                AppName = finding.AppName,
                Severity = finding.Severity,
                Summary = finding.Summary,
                EstimatedDrainPercent = finding.EstimatedDrainPercent,
                BackgroundUsageMinutes = finding.BackgroundUsageMinutes,
                ForegroundServiceMinutes = finding.ForegroundServiceMinutes,
                UsageSource = finding.UsageSource,
                IsOfficialPowerData = finding.IsOfficialPowerData,
                DetectedAt = DateTime.UtcNow,
            };

            await _database.SaveBackgroundDrainEventAsync(newEvent);
            newFindings.Add(finding);
        }

        if (newFindings.Count > 0)
        {
            await DeliverGuardianAlertAsync(
                newFindings[0],
                newFindings.Count - 1,
                settings.NotificationsEnabled);
        }

        return newFindings;
    }

    private async Task<bool> HasEnoughBatterySignalAsync(DateTime observedSince)
    {
        var records = await _database.GetBatteryRecordsAsync(observedSince, DateTime.UtcNow);
        if (records.Count < 2)
            return false;

        var drain = records[0].BatteryLevel - records[^1].BatteryLevel;
        return drain >= 3;
    }

    private async Task DeliverGuardianAlertAsync(
        BackgroundDrainFinding finding,
        int additionalCount,
        bool notificationsEnabled)
    {
        var deliveryMode = BatteryGuardianDeliveryPolicy.Decide(
            _appVisibility.IsInForeground,
            notificationsEnabled,
            _notificationService.CanNotify);

        if (deliveryMode == BatteryGuardianDeliveryMode.InAppDialog)
        {
            await ShowGuardianDialogAsync(finding, additionalCount);
            return;
        }

        if (deliveryMode == BatteryGuardianDeliveryMode.LocalNotification)
        {
            await _notificationService.NotifyAsync(finding, additionalCount);
        }
    }

    private static async Task ShowGuardianDialogAsync(BackgroundDrainFinding finding, int additionalCount)
    {
        if (Shell.Current is null)
            return;

        var suffix = additionalCount > 0
            ? $"\n\n{additionalCount} more app(s) also look suspicious."
            : string.Empty;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Shell.Current.DisplayAlert(
                $"Battery Guardian: {finding.AppName}",
                $"{finding.Summary}{suffix}",
                "OK");
        });
    }
}
