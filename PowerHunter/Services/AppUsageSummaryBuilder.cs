using PowerHunter.Models;

namespace PowerHunter.Services;

/// <summary>
/// Converts raw OS app activity into persisted daily usage records.
/// Official per-app power stats are preferred when the platform exposes them.
/// </summary>
public static class AppUsageSummaryBuilder
{
    private const long MinTrackedActivityMs = 60_000;

    public static List<AppUsageRecord> Build(IEnumerable<RawAppUsage> rawUsage, DateTime syncedAtUtc)
    {
        var filtered = rawUsage
            .Where(record =>
                record.ForegroundTimeMs + record.BackgroundVisibleTimeMs + record.ForegroundServiceTimeMs >= MinTrackedActivityMs
                || record.ConsumedPowerMah.GetValueOrDefault() > 0)
            .ToList();

        if (filtered.Count == 0)
            return [];

        var totalOfficialPowerMah = filtered
            .Sum(record => Math.Max(record.ConsumedPowerMah.GetValueOrDefault(), 0));

        if (totalOfficialPowerMah > 0)
        {
            return filtered
                .Select(record => BuildOfficialPowerRecord(record, totalOfficialPowerMah, syncedAtUtc))
                .OrderByDescending(record => record.UsagePercentage)
                .ThenByDescending(record => record.PowerConsumedMah)
                .ToList();
        }

        var appSummaries = filtered.Select(record =>
        {
            var foregroundMinutes = record.ForegroundTimeMs / 60_000.0;
            var backgroundMinutes = record.BackgroundVisibleTimeMs / 60_000.0;
            var foregroundServiceMinutes = record.ForegroundServiceTimeMs / 60_000.0;
            var effectiveMinutes = foregroundMinutes
                                 + (backgroundMinutes * 0.65)
                                 + (foregroundServiceMinutes * 0.9);

            return new
            {
                Record = record,
                ForegroundMinutes = foregroundMinutes,
                BackgroundMinutes = backgroundMinutes,
                ForegroundServiceMinutes = foregroundServiceMinutes,
                EffectiveMinutes = effectiveMinutes,
            };
        }).ToList();

        var totalEffectiveMinutes = appSummaries.Sum(summary => summary.EffectiveMinutes);
        if (totalEffectiveMinutes <= 0)
            return [];

        return appSummaries
            .Select(summary =>
            {
                var category = AppCategoryResolver.Resolve(summary.Record);
                return new AppUsageRecord
                {
                    AppId = summary.Record.PackageName,
                    AppName = summary.Record.AppLabel,
                    Category = category,
                    OriginalCategory = AppCategoryResolver.ResolveOriginalCategory(summary.Record),
                    UsagePercentage = Math.Round((summary.EffectiveMinutes / totalEffectiveMinutes) * 100, 1),
                    UsageMinutes = Math.Round(summary.ForegroundMinutes, 1),
                    BackgroundUsageMinutes = Math.Round(summary.BackgroundMinutes, 1),
                    ForegroundServiceMinutes = Math.Round(summary.ForegroundServiceMinutes, 1),
                    PowerConsumedMah = 0,
                    UsageSource = AppUsageSourceKind.SystemUsageStats,
                    IsOfficialPowerData = false,
                    LastSyncedAtUtc = syncedAtUtc,
                    Date = summary.Record.Date.Date,
                };
            })
            .OrderByDescending(record => record.UsagePercentage)
            .ThenByDescending(record => record.BackgroundUsageMinutes)
            .ToList();
    }

    private static AppUsageRecord BuildOfficialPowerRecord(
        RawAppUsage record,
        double totalOfficialPowerMah,
        DateTime syncedAtUtc)
    {
        var foregroundMinutes = record.ForegroundTimeMs / 60_000.0;
        var backgroundMinutes = record.BackgroundVisibleTimeMs / 60_000.0;
        var foregroundServiceMinutes = record.ForegroundServiceTimeMs / 60_000.0;
        var powerConsumedMah = Math.Round(Math.Max(record.ConsumedPowerMah.GetValueOrDefault(), 0), 3);

        var category = AppCategoryResolver.Resolve(record);

        return new AppUsageRecord
        {
            AppId = record.PackageName,
            AppName = record.AppLabel,
            Category = category,
            OriginalCategory = AppCategoryResolver.ResolveOriginalCategory(record),
            UsagePercentage = Math.Round((powerConsumedMah / totalOfficialPowerMah) * 100, 1),
            UsageMinutes = Math.Round(foregroundMinutes, 1),
            BackgroundUsageMinutes = Math.Round(backgroundMinutes, 1),
            ForegroundServiceMinutes = Math.Round(foregroundServiceMinutes, 1),
            PowerConsumedMah = powerConsumedMah,
            UsageSource = AppUsageSourceKind.OfficialBatteryStats,
            IsOfficialPowerData = true,
            LastSyncedAtUtc = syncedAtUtc,
            Date = record.Date.Date,
        };
    }
}
