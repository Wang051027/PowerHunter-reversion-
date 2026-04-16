using CommunityToolkit.Maui;
using LiveChartsCore.SkiaSharpView.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace PowerHunter;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .UseLiveCharts()
            .ConfigureFonts(fonts =>
            {
                // Uses system default fonts — no custom fonts bundled
            });

        // Database
        builder.Services.AddSingleton<PowerHunterDatabase>();
        builder.Services.AddSingleton<DatePartitionStorageService>();
        builder.Services.AddSingleton<DataLifecycleService>();
        builder.Services.AddSingleton<AppVisibilityService>();
        builder.Services.AddSingleton<IAppVisibilityService>(sp => sp.GetRequiredService<AppVisibilityService>());

        // Platform-specific app usage collection
#if ANDROID
        builder.Services.AddSingleton<IAppUsageCollector, PowerHunter.Platforms.Android.Services.AndroidAppUsageCollector>();
        builder.Services.AddSingleton<IAppIconService, PowerHunter.Platforms.Android.Services.AndroidAppIconService>();
        builder.Services.AddSingleton<IUsageStatsPermission, PowerHunter.Platforms.Android.Services.AndroidUsageStatsPermission>();
        builder.Services.AddSingleton<IAlertNotificationService, PowerHunter.Platforms.Android.Services.AndroidAlertNotificationService>();
        builder.Services.AddSingleton<IGuardianNotificationService, PowerHunter.Platforms.Android.Services.AndroidGuardianNotificationService>();
#else
        builder.Services.AddSingleton<IAppUsageCollector, NullAppUsageCollector>();
        builder.Services.AddSingleton<IAppIconService, NullAppIconService>();
        builder.Services.AddSingleton<IUsageStatsPermission, NullUsageStatsPermission>();
        builder.Services.AddSingleton<IAlertNotificationService, NullAlertNotificationService>();
        builder.Services.AddSingleton<IGuardianNotificationService, NullGuardianNotificationService>();
#endif

        // Services
        builder.Services.AddSingleton<IBatteryService, BatteryService>();
        builder.Services.AddSingleton<IAlertService, AlertService>();
        builder.Services.AddSingleton<AppUsageAlertEvaluator>();
        builder.Services.AddSingleton<PowerEstimationService>();
        builder.Services.AddSingleton<BatteryGuardianService>();

        // ViewModels
        builder.Services.AddTransient<StatsViewModel>();
        builder.Services.AddTransient<AppsViewModel>();
        builder.Services.AddTransient<MonitorViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Views
        builder.Services.AddTransient<StatsPage>();
        builder.Services.AddTransient<AppsPage>();
        builder.Services.AddTransient<MonitorPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}
