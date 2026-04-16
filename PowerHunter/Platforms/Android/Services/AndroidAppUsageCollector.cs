using Android.App;
using Android.App.Usage;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace PowerHunter.Platforms.Android.Services;

/// <summary>
/// Android implementation of IAppUsageCollector using UsageStatsManager.
///
/// Requires PACKAGE_USAGE_STATS permission which the user must manually grant:
///   Settings > Apps > Special access > Usage access
///
/// Returns per-app foreground time for the requested period.
/// On OEM skins (MIUI, EMUI, ColorOS) behavior may vary — always handles null/empty gracefully.
/// </summary>
public sealed class AndroidAppUsageCollector : IAppUsageCollector
{
    private readonly UsageStatsManager? _usageStatsManager;
    private readonly PackageManager? _packageManager;

    public AndroidAppUsageCollector()
    {
        var context = global::Android.App.Application.Context;
        _usageStatsManager = context.GetSystemService(Context.UsageStatsService) as UsageStatsManager;
        _packageManager = context.PackageManager;
    }

    public bool IsAvailable
    {
        get
        {
            if (_usageStatsManager is null) return false;

            // Pragmatic permission check: try a small query and see if data comes back.
            // This is more reliable across API levels than AppOpsManager checks.
            try
            {
                var now = Java.Lang.JavaSystem.CurrentTimeMillis();
                var oneHourAgo = now - 3_600_000;
                var stats = _usageStatsManager.QueryUsageStats(UsageStatsInterval.Daily, oneHourAgo, now);
                return stats is not null && stats.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public Task<List<RawAppUsage>> CollectAsync(DateTime since)
    {
        var result = new List<RawAppUsage>();

        if (_usageStatsManager is null || _packageManager is null)
            return Task.FromResult(result);

        try
        {
            var sinceMs = new DateTimeOffset(since.ToUniversalTime()).ToUnixTimeMilliseconds();
            var nowMs = Java.Lang.JavaSystem.CurrentTimeMillis();

            var stats = _usageStatsManager.QueryUsageStats(UsageStatsInterval.Daily, sinceMs, nowMs);
            if (stats is null || stats.Count == 0)
                return Task.FromResult(result);

            // Aggregate by package name (UsageStatsManager may return multiple entries per app)
            var aggregated = new Dictionary<string, long>();
            foreach (var stat in stats)
            {
                if (stat.PackageName is null) continue;
                var fg = stat.TotalTimeInForeground;
                if (fg <= 0) continue;

                if (aggregated.TryGetValue(stat.PackageName, out var existing))
                    aggregated[stat.PackageName] = existing + fg;
                else
                    aggregated[stat.PackageName] = fg;
            }

            foreach (var (packageName, foregroundMs) in aggregated)
            {
                long visibleMs = 0;
                long foregroundServiceMs = 0;

                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                {
                    foreach (var stat in stats.Where(s => s.PackageName == packageName))
                    {
                        visibleMs += stat.TotalTimeVisible;
                        foregroundServiceMs += stat.TotalTimeForegroundServiceUsed;
                    }
                }

                var label = ResolveAppLabel(packageName);
                var categorySignal = BuildCategorySignal(packageName, foregroundMs, visibleMs, foregroundServiceMs);
                result.Add(new RawAppUsage(
                    packageName,
                    label,
                    foregroundMs,
                    since.Date,
                    VisibleTimeMs: visibleMs,
                    ForegroundServiceTimeMs: foregroundServiceMs,
                    CategorySignal: categorySignal));
            }
        }
        catch (Java.Lang.SecurityException)
        {
            // Permission not granted — return empty
        }
        catch (Exception)
        {
            // Unexpected error on specific OEM — return empty
        }

        return Task.FromResult(
            result.OrderByDescending(r => r.ForegroundTimeMs).ToList()
        );
    }

    private AppCategorySignal BuildCategorySignal(
        string packageName,
        long foregroundMs,
        long visibleMs,
        long foregroundServiceMs)
    {
        string? primaryCategoryHint = null;
        var behaviorTags = new List<string>();

        try
        {
            var appInfo = _packageManager!.GetApplicationInfo(packageName, PackageInfoFlags.MatchDefaultOnly);
            primaryCategoryHint = MapApplicationCategory(appInfo.Category);

            var requestedPermissions = GetRequestedPermissions(packageName);

            if (MatchesLauncherCategory(packageName, Intent.CategoryAppBrowser) ||
                MatchesLauncherCategory(packageName, Intent.CategoryAppMaps) ||
                MatchesLauncherCategory(packageName, Intent.CategoryAppCalculator) ||
                MatchesLauncherCategory(packageName, Intent.CategoryAppCalendar) ||
                MatchesLauncherCategory(packageName, Intent.CategoryAppEmail) ||
                MatchesLauncherCategory(packageName, Intent.CategoryAppFiles))
            {
                behaviorTags.Add("launcher-tools");
            }

            if (MatchesLauncherCategory(packageName, Intent.CategoryAppMusic))
            {
                behaviorTags.Add("launcher-music");
            }

            if (MatchesLauncherCategory(packageName, Intent.CategoryAppGallery))
            {
                behaviorTags.Add("launcher-video");
            }

            if (MatchesLauncherCategory(packageName, Intent.CategoryAppMessaging) ||
                MatchesLauncherCategory(packageName, Intent.CategoryAppContacts))
            {
                behaviorTags.Add("launcher-social");
            }

            if (foregroundServiceMs >= 10 * 60_000 &&
                requestedPermissions.Any(permission => IsAudioPermission(permission)))
            {
                behaviorTags.Add("media-playback-service");
            }

            if (visibleMs >= foregroundMs + (10 * 60_000) &&
                requestedPermissions.Any(permission => IsVideoPermission(permission)))
            {
                behaviorTags.Add("immersive-video");
            }

            if (requestedPermissions.Any(permission => IsMessagingPermission(permission)) ||
                SupportsShareTargets(packageName))
            {
                behaviorTags.Add("messaging-capability");
            }

            if (requestedPermissions.Any(permission => IsToolingPermission(permission)))
            {
                behaviorTags.Add("tooling-capability");
            }
        }
        catch (PackageManager.NameNotFoundException)
        {
            // App disappeared mid-query.
        }
        catch
        {
            // Best-effort metadata enrichment only.
        }

        return new AppCategorySignal(
            PrimaryCategoryHint: primaryCategoryHint,
            BehaviorTags: behaviorTags.Count == 0 ? null : behaviorTags.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private string ResolveAppLabel(string packageName)
    {
        try
        {
            var appInfo = _packageManager!.GetApplicationInfo(packageName, PackageInfoFlags.MatchDefaultOnly);
            if (appInfo is not null)
            {
                var label = _packageManager.GetApplicationLabel(appInfo);
                if (!string.IsNullOrEmpty(label))
                    return label;
            }
        }
        catch (PackageManager.NameNotFoundException)
        {
            // App uninstalled — use package name
        }
        catch
        {
            // Other error — fallback
        }

        // Fallback: extract readable name from package
        // "com.tencent.ig" -> "tencent.ig"
        var parts = packageName.Split('.');
        return parts.Length >= 2
            ? string.Join('.', parts.Skip(1))
            : packageName;
    }

    private string? MapApplicationCategory(ApplicationCategories category)
    {
        if (category == ApplicationCategories.Game)
            return "game";

        if (category == ApplicationCategories.Audio)
            return "audio";

        if (category == ApplicationCategories.Video)
            return "video";

        if (category == ApplicationCategories.Image)
            return "image";

        if (category == ApplicationCategories.Social)
            return "social";

        if (category == ApplicationCategories.Productivity)
            return "productivity";

        if (category == ApplicationCategories.Accessibility)
            return "accessibility";

        if (category == ApplicationCategories.Maps)
            return "maps";

        if (category == ApplicationCategories.News)
            return "news";

        return null;
    }

    private IReadOnlyList<string> GetRequestedPermissions(string packageName)
    {
        try
        {
            var packageInfo = _packageManager!.GetPackageInfo(packageName, PackageInfoFlags.Permissions);
            return packageInfo?.RequestedPermissions?.Where(permission => !string.IsNullOrWhiteSpace(permission)).ToList()
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    private bool MatchesLauncherCategory(string packageName, string launcherCategory)
    {
        try
        {
            var intent = new Intent(Intent.ActionMain);
            intent.AddCategory(launcherCategory);
            intent.SetPackage(packageName);

            var matches = _packageManager!.QueryIntentActivities(intent, PackageInfoFlags.MatchDefaultOnly);
            return matches is not null && matches.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private bool SupportsShareTargets(string packageName)
    {
        try
        {
            var intent = new Intent(Intent.ActionSend);
            intent.SetType("text/plain");
            intent.SetPackage(packageName);

            var matches = _packageManager!.QueryIntentActivities(intent, PackageInfoFlags.MatchDefaultOnly);
            return matches is not null && matches.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAudioPermission(string permission) =>
        permission.Contains("MEDIA_PLAYBACK", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("READ_MEDIA_AUDIO", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("MODIFY_AUDIO_SETTINGS", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("RECORD_AUDIO", StringComparison.OrdinalIgnoreCase);

    private static bool IsVideoPermission(string permission) =>
        permission.Contains("READ_MEDIA_VIDEO", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("CAMERA", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("FOREGROUND_SERVICE_CAMERA", StringComparison.OrdinalIgnoreCase);

    private static bool IsMessagingPermission(string permission) =>
        permission.Contains("READ_CONTACTS", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("WRITE_CONTACTS", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("READ_SMS", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("SEND_SMS", StringComparison.OrdinalIgnoreCase);

    private static bool IsToolingPermission(string permission) =>
        permission.Contains("PACKAGE_USAGE_STATS", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("BIND_ACCESSIBILITY_SERVICE", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("SYSTEM_ALERT_WINDOW", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("WRITE_SETTINGS", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("MANAGE_EXTERNAL_STORAGE", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("ACCESS_FINE_LOCATION", StringComparison.OrdinalIgnoreCase) ||
        permission.Contains("ACCESS_COARSE_LOCATION", StringComparison.OrdinalIgnoreCase);
}
