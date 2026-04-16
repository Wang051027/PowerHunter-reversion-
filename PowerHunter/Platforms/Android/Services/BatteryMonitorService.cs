using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using System.Runtime.Versioning;

namespace PowerHunter.Platforms.Android.Services;

/// <summary>
/// Android foreground service for continuous battery monitoring.
/// Records battery snapshots every 15 minutes and evaluates alerts.
/// Runs as a sticky foreground service so Android doesn't kill it.
/// </summary>
#pragma warning disable CA1416 // Manifest metadata for Android 14+ foreground-service categorization.
[Service(
    ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeSpecialUse,
    Exported = false)]
#pragma warning restore CA1416
public sealed class BatteryMonitorService : Service
{
    private const int NotificationId = 9001;
    private const string ChannelId = "battery_monitor";

    private Timer? _snapshotTimer;
    private Timer? _alertTimer;
    private int _isSnapshotRunning;
    private int _isAlertCheckRunning;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();
        var notification = BuildNotification("Monitoring battery usage...");

        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            StartForegroundSpecialUse(notification);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        StartPeriodicMonitoring();

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        _snapshotTimer?.Dispose();
        _snapshotTimer = null;
        _alertTimer?.Dispose();
        _alertTimer = null;
        base.OnDestroy();
    }

    private void StartPeriodicMonitoring()
    {
        _snapshotTimer?.Dispose();
        _alertTimer?.Dispose();

        _snapshotTimer = new Timer(
            async _ => await RunSnapshotCycleAsync(),
            null,
            TimeSpan.Zero,
            BatteryRefreshDefaults.BackgroundSnapshotInterval);

        _alertTimer = new Timer(
            async _ => await RunAlertCheckCycleAsync(),
            null,
            TimeSpan.Zero,
            BatteryRefreshDefaults.AlertCheckInterval);
    }

    private async Task RunSnapshotCycleAsync()
    {
        if (Interlocked.Exchange(ref _isSnapshotRunning, 1) == 1)
            return;

        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is null) return;

            var batteryService = services.GetService<IBatteryService>();
            if (batteryService is null) return;

            var record = await batteryService.RecordSnapshotAsync();

            // Update notification with current battery level
            UpdateNotification($"Battery: {record.BatteryLevel:F0}% | {record.ChargingState}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BatteryMonitorService] Snapshot error: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _isSnapshotRunning, 0);
        }
    }

    private async Task RunAlertCheckCycleAsync()
    {
        if (Interlocked.Exchange(ref _isAlertCheckRunning, 1) == 1)
            return;

        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is null) return;

            var database = services.GetService<PowerHunterDatabase>();
            var powerEstimation = services.GetService<PowerEstimationService>();
            var alertEvaluator = services.GetService<AppUsageAlertEvaluator>();

            if (database is null || powerEstimation is null || alertEvaluator is null)
                return;

            var settings = await database.GetSettingsAsync();
            var alerts = await database.GetAlertsAsync();
            if (!AlertPollingPolicy.ShouldPoll(settings, alerts, powerEstimation.IsCollectionAvailable))
                return;

            var records = await powerEstimation.CollectAndPersistAsync(DateTime.UtcNow.Date);
            await alertEvaluator.EvaluateQuietlyAsync(records);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BatteryMonitorService] Alert check error: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _isAlertCheckRunning, 0);
        }
    }

    private void CreateNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        CreateNotificationChannelCore();
    }

    [SupportedOSPlatform("android26.0")]
    private void CreateNotificationChannelCore()
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager?.GetNotificationChannel(ChannelId) is not null)
            return;

        var channel = new NotificationChannel(
            ChannelId,
            "Battery Monitor",
            NotificationImportance.Low)
        {
            Description = "Continuous battery monitoring service",
        };
        channel.SetShowBadge(false);

        manager?.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification(string text)
    {
        // Launch the app when notification is tapped
        var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop);
        var pendingIntent = PendingIntent.GetActivity(
            this, 0, intent, GetPendingIntentFlags());

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("Power Hunter")
            .SetContentText(text)
            .SetSmallIcon(_Microsoft.Android.Resource.Designer.ResourceConstant.Mipmap.appicon_foreground)
            .SetOngoing(true)
            .SetContentIntent(pendingIntent)
            .Build();
    }

    private void UpdateNotification(string text)
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.Notify(NotificationId, BuildNotification(text));
    }

    [SupportedOSPlatform("android34.0")]
    private void StartForegroundSpecialUse(Notification notification)
    {
        StartForeground(
            NotificationId,
            notification,
            global::Android.Content.PM.ForegroundService.TypeSpecialUse);
    }

    private static PendingIntentFlags GetPendingIntentFlags()
    {
        var flags = PendingIntentFlags.UpdateCurrent;

        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            flags |= PendingIntentFlags.Immutable;
        }

        return flags;
    }
}
