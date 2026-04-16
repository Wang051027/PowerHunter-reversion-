namespace PowerHunter.Services;

/// <summary>
/// Alert management service.
/// Evaluates user-configured thresholds against the latest per-app battery usage share
/// and emits system notifications when thresholds are crossed.
/// </summary>
public sealed class AlertService : IAlertService
{
    private readonly PowerHunterDatabase _database;
    private readonly IAlertNotificationService _notificationService;

    public AlertService(
        PowerHunterDatabase database,
        IAlertNotificationService notificationService)
    {
        _database = database;
        _notificationService = notificationService;
    }

    public async Task<BatteryAlert> CreateAlertAsync(string title, string description, double thresholdPercent)
    {
        var alert = new BatteryAlert
        {
            Title = title,
            Description = description,
            ThresholdPercent = thresholdPercent,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _database.SaveAlertAsync(alert);
        return alert;
    }

    public async Task ToggleAlertAsync(int alertId, bool isEnabled)
    {
        var alerts = await _database.GetAlertsAsync();
        var alert = alerts.FirstOrDefault(a => a.Id == alertId);
        if (alert is null) return;

        alert.IsEnabled = isEnabled;
        await _database.SaveAlertAsync(alert);
    }

    public async Task DeleteAlertAsync(int alertId)
    {
        await _database.DeleteAlertAsync(alertId);
    }

    public async Task<List<BatteryAlert>> GetAlertsAsync()
    {
        return await _database.GetAlertsAsync();
    }

    public async Task EvaluateAlertsAsync(IEnumerable<AppUsageRecord> usageRecords)
    {
        var settings = await _database.GetSettingsAsync();
        if (!settings.GuardianEnabled || !settings.NotificationsEnabled || !_notificationService.CanNotify)
            return;

        var usageList = usageRecords.ToList();
        if (usageList.Count == 0)
            return;

        var alerts = await _database.GetAlertsAsync();
        var now = DateTime.UtcNow;

        foreach (var alert in alerts)
        {
            var triggeredApp = AlertTriggerPolicy.FindTriggeredApp(
                    alert,
                    usageList,
                    settings.GuardianEnabled,
                    settings.NotificationsEnabled,
                    _notificationService.CanNotify,
                    now);
            if (triggeredApp is null)
                continue;

            await _notificationService.NotifyAsync(alert, triggeredApp);

            alert.LastTriggeredAt = now;
            await _database.SaveAlertAsync(alert);
        }
    }
}
