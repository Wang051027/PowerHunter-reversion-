using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using CommunityToolkit.Mvvm.Messaging;
using PowerHunter.Platforms.Android.Services;
using System.Runtime.Versioning;

namespace PowerHunter;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize
                         | ConfigChanges.Orientation
                         | ConfigChanges.UiMode
                         | ConfigChanges.ScreenLayout
                         | ConfigChanges.SmallestScreenSize
                         | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const string NotificationPermission = "android.permission.POST_NOTIFICATIONS";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Request notification permission on Android 13+
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            RequestNotificationPermission();
        }

        // Start battery monitoring foreground service
        StartBatteryMonitorService();
    }

    protected override void OnResume()
    {
        base.OnResume();
        GetAppVisibilityService()?.MarkForeground();

        // Re-check usage stats permission when user returns from Settings
        WeakReferenceMessenger.Default.Send(new PermissionRefreshMessage());
    }

    protected override void OnPause()
    {
        GetAppVisibilityService()?.MarkBackground();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        GetAppVisibilityService()?.MarkBackground();
        base.OnDestroy();
    }

    private void StartBatteryMonitorService()
    {
        var intent = new Intent(this, typeof(BatteryMonitorService));

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            StartForegroundBatteryMonitorService(intent);
        }
        else
        {
            StartService(intent);
        }
    }

    [SupportedOSPlatform("android23.0")]
    private void RequestNotificationPermission()
    {
        RequestPermissions([NotificationPermission], 0);
    }

    [SupportedOSPlatform("android26.0")]
    private void StartForegroundBatteryMonitorService(Intent intent)
    {
        StartForegroundService(intent);
    }

    private AppVisibilityService? GetAppVisibilityService()
    {
        return IPlatformApplication.Current?.Services.GetService<AppVisibilityService>();
    }
}
