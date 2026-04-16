using Android;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using PowerHunter.Models;
using System.Runtime.Versioning;

namespace PowerHunter.Platforms.Android.Services;

public sealed class AndroidAlertNotificationService : IAlertNotificationService
{
    private const string ChannelId = "smart_alerts";
    private const string NotificationPermission = "android.permission.POST_NOTIFICATIONS";
    private readonly Context _context;

    public AndroidAlertNotificationService()
    {
        _context = global::Android.App.Application.Context;
    }

    public bool CanNotify
    {
        get
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
                ContextCompat.CheckSelfPermission(_context, NotificationPermission) != global::Android.Content.PM.Permission.Granted)
            {
                return false;
            }

            return NotificationManagerCompat.From(_context).AreNotificationsEnabled();
        }
    }

    public Task NotifyAsync(BatteryAlert alert, AppUsageRecord triggeredApp)
    {
        if (!CanNotify)
            return Task.CompletedTask;

        EnsureChannel();

        var intent = new Intent(_context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop | ActivityFlags.NewTask);

        var pendingIntent = PendingIntent.GetActivity(
            _context,
            3001,
            intent,
            GetPendingIntentFlags());

        var metricLabel = triggeredApp.IsOfficialPowerData ? "battery usage share" : "usage impact share";
        var message = $"{triggeredApp.AppName} reached {triggeredApp.UsagePercentage:F1}% {metricLabel}, above the {alert.ThresholdPercent:F0}% threshold.";

        var notification = new NotificationCompat.Builder(_context, ChannelId)
            .SetSmallIcon(_Microsoft.Android.Resource.Designer.ResourceConstant.Mipmap.appicon_foreground)
            .SetContentTitle(alert.Title)
            .SetContentText(message)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(message))
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent)
            .Build();

        NotificationManagerCompat.From(_context).Notify(CreateNotificationId(alert, triggeredApp), notification);
        return Task.CompletedTask;
    }

    private void EnsureChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        EnsureChannelCore();
    }

    [SupportedOSPlatform("android26.0")]
    private void EnsureChannelCore()
    {
        var manager = (NotificationManager?)_context.GetSystemService(Context.NotificationService);
        if (manager?.GetNotificationChannel(ChannelId) is not null)
            return;

        var channel = new NotificationChannel(
            ChannelId,
            "Smart Alerts",
            NotificationImportance.High)
        {
            Description = "Threshold-based battery alerts from Power Hunter.",
        };

        manager?.CreateNotificationChannel(channel);
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

    private static int CreateNotificationId(BatteryAlert alert, AppUsageRecord triggeredApp)
    {
        var id = HashCode.Combine(alert.Id, triggeredApp.AppId, DateTime.UtcNow.Minute, DateTime.UtcNow.Second);
        return Math.Abs(id == int.MinValue ? int.MaxValue : id);
    }
}
