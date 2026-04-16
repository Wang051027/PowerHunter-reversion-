using Android.App;
using Android.App.Usage;
using Android.Content;
using Android.Provider;
using System.Runtime.Versioning;

namespace PowerHunter.Platforms.Android.Services;

/// <summary>
/// Android implementation of IUsageStatsPermission.
///
/// PACKAGE_USAGE_STATS is a special permission that cannot be granted via
/// the standard permission dialog. The user must navigate to:
///   Settings > Apps > Special access > Usage access
/// and manually toggle access for this app.
///
/// This service checks permission status and opens the Settings screen.
/// </summary>
public sealed class AndroidUsageStatsPermission : IUsageStatsPermission
{
    public bool IsSupported => true;

    public bool IsGranted
    {
        get
        {
            try
            {
                var context = global::Android.App.Application.Context;
                var appOps = context.GetSystemService(Context.AppOpsService) as AppOpsManager;
                if (appOps is not null)
                {
                    var mode = GetUsageStatsMode(appOps, context.PackageName ?? string.Empty);

                    if (mode == AppOpsManagerMode.Allowed)
                        return true;
                }

                return HasUsageStatsData(context);
            }
            catch
            {
                return false;
            }
        }
    }

    public Task RequestAsync()
    {
        var context = global::Android.App.Application.Context;

        foreach (var intent in CreateRequestIntents(context))
        {
            try
            {
                var packageManager = context.PackageManager;
                if (packageManager is null || intent.ResolveActivity(packageManager) is null)
                    continue;

                context.StartActivity(intent);
                return Task.CompletedTask;
            }
            catch (ActivityNotFoundException)
            {
                // Try the next fallback intent.
            }
            catch
            {
                // Try the next fallback intent.
            }
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<Intent> CreateRequestIntents(Context context)
    {
        var usageAccessIntent = new Intent(Settings.ActionUsageAccessSettings);
        usageAccessIntent.AddFlags(ActivityFlags.NewTask);
        yield return usageAccessIntent;

        var appDetailsIntent = new Intent(Settings.ActionApplicationDetailsSettings);
        appDetailsIntent.SetData(global::Android.Net.Uri.Parse($"package:{context.PackageName}"));
        appDetailsIntent.AddFlags(ActivityFlags.NewTask);
        yield return appDetailsIntent;

        var settingsIntent = new Intent(Settings.ActionSettings);
        settingsIntent.AddFlags(ActivityFlags.NewTask);
        yield return settingsIntent;
    }

    private static bool HasUsageStatsData(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(22))
            return false;

        return HasUsageStatsDataCore(context);
    }

    [SupportedOSPlatform("android22.0")]
    private static bool HasUsageStatsDataCore(Context context)
    {
        var usageStatsManager = context.GetSystemService(Context.UsageStatsService) as UsageStatsManager;
        if (usageStatsManager is null)
            return false;

        var now = Java.Lang.JavaSystem.CurrentTimeMillis();
        var stats = usageStatsManager.QueryUsageStats(UsageStatsInterval.Daily, now - 3_600_000, now);
        return stats is not null && stats.Count > 0;
    }

    private static AppOpsManagerMode GetUsageStatsMode(AppOpsManager appOps, string packageName)
    {
        var uid = global::Android.OS.Process.MyUid();

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            return GetUsageStatsModeApi29(appOps, uid, packageName);
        }

#pragma warning disable CA1422 // Used only on Android 21-28 where this API is the supported option.
        return appOps.CheckOpNoThrow(AppOpsManager.OpstrGetUsageStats, uid, packageName);
#pragma warning restore CA1422
    }

    [SupportedOSPlatform("android29.0")]
    private static AppOpsManagerMode GetUsageStatsModeApi29(AppOpsManager appOps, int uid, string packageName)
    {
        return appOps.UnsafeCheckOpNoThrow(AppOpsManager.OpstrGetUsageStats, uid, packageName);
    }
}
