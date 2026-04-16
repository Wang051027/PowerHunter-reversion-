using Android;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using PowerHunter.Models;
using System.Runtime.Versioning;

namespace PowerHunter.Platforms.Android.Services;

public sealed class AndroidGuardianNotificationService : IGuardianNotificationService
{
    private const string ChannelId = "guardian_alerts";
    private const string NotificationPermission = "android.permission.POST_NOTIFICATIONS";
    private readonly Context _context;

    public AndroidGuardianNotificationService()
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

    public Task NotifyAsync(BackgroundDrainFinding finding, int additionalCount)
    {
        if (!CanNotify)
            return Task.CompletedTask;

        EnsureChannel();

        var intent = new Intent(_context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop | ActivityFlags.NewTask);

        var pendingIntent = PendingIntent.GetActivity(
            _context,
            2001,
            intent,
            GetPendingIntentFlags());

        var suffix = additionalCount > 0
            ? $"\n\n{additionalCount} more app(s) also look suspicious."
            : string.Empty;
        var message = $"{finding.Summary}{suffix}";

        var notification = new NotificationCompat.Builder(_context, ChannelId)
            .SetSmallIcon(_Microsoft.Android.Resource.Designer.ResourceConstant.Mipmap.appicon_foreground)
            .SetContentTitle($"Battery Guardian: {finding.AppName}")
            .SetContentText(message)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(message))
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent)
            .Build();

        NotificationManagerCompat.From(_context).Notify(CreateNotificationId(finding), notification);
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
            "Battery Guardian Alerts",
            NotificationImportance.High)
        {
            Description = "Alerts for suspicious background battery drain.",
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

    private static int CreateNotificationId(BackgroundDrainFinding finding)
    {
        var id = HashCode.Combine(finding.AppId, DateTime.UtcNow.Minute, DateTime.UtcNow.Second);
        return Math.Abs(id == int.MinValue ? int.MaxValue : id);
    }
}
