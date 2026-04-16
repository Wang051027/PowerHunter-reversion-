using System.Globalization;
using PowerHunter.Models;

namespace PowerHunter.Services;

/// <summary>
/// Builds battery usage trend points from recorded system battery snapshots.
/// Day view shows today's cumulative battery used so far.
/// Week view shows total battery used per local day.
/// </summary>
public static class BatteryTrendBuilder
{
    public static (DateTime FromUtc, DateTime ToUtc) GetUtcBoundsForLocalDate(
        DateTime localDate,
        TimeZoneInfo? timeZone = null)
    {
        var zone = timeZone ?? TimeZoneInfo.Local;
        var dayStartLocal = localDate.Date;
        var nextDayStartLocal = dayStartLocal.AddDays(1);

        return (
            ConvertLocalBoundaryToUtc(dayStartLocal, zone),
            ConvertLocalBoundaryToUtc(nextDayStartLocal, zone));
    }

    public static List<TrendPoint> BuildIntradayUsageTrend(
        IEnumerable<BatteryRecord> records,
        DateTime localDate,
        TimeZoneInfo? timeZone = null)
    {
        var zone = timeZone ?? TimeZoneInfo.Local;
        var dayRecords = records
            .Select(record => new
            {
                Record = record,
                LocalRecordedAt = ConvertUtcSnapshotToLocal(record.RecordedAt, zone),
            })
            .Where(entry => entry.LocalRecordedAt.Date == localDate.Date)
            .OrderBy(entry => entry.LocalRecordedAt)
            .ToList();

        if (dayRecords.Count == 0)
            return [];

        var trendPoints = new List<TrendPoint>(dayRecords.Count);
        double cumulativeBatteryUsed = 0;

        trendPoints.Add(new TrendPoint(
            dayRecords[0].LocalRecordedAt.ToString("HH:mm", CultureInfo.InvariantCulture),
            0,
            dayRecords[0].LocalRecordedAt));

        for (int i = 1; i < dayRecords.Count; i++)
        {
            cumulativeBatteryUsed += CalculateDrop(
                dayRecords[i - 1].Record.BatteryLevel,
                dayRecords[i].Record.BatteryLevel);

            trendPoints.Add(new TrendPoint(
                dayRecords[i].LocalRecordedAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                Math.Round(cumulativeBatteryUsed, 1),
                dayRecords[i].LocalRecordedAt));
        }

        return trendPoints;
    }

    public static List<TrendPoint> BuildDailyUsageTrend(
        IEnumerable<BatteryRecord> records,
        DateTime fromLocalDate,
        DateTime toLocalDate,
        TimeZoneInfo? timeZone = null,
        DateTime? todayLocalDateOverride = null)
    {
        var zone = timeZone ?? TimeZoneInfo.Local;
        var todayLocal = todayLocalDateOverride?.Date ?? ConvertUtcSnapshotToLocal(DateTime.UtcNow, zone).Date;

        return records
            .Select(record => new
            {
                Record = record,
                LocalRecordedAt = ConvertUtcSnapshotToLocal(record.RecordedAt, zone),
            })
            .Where(entry =>
                entry.LocalRecordedAt.Date >= fromLocalDate.Date &&
                entry.LocalRecordedAt.Date <= toLocalDate.Date)
            .GroupBy(entry => entry.LocalRecordedAt.Date)
            .OrderBy(group => group.Key)
            .Select(group => new TrendPoint(
                group.Key == todayLocal
                    ? "Today"
                    : group.Key.ToString("M/d ddd", CultureInfo.InvariantCulture),
                CalculateBatteryUsed(group.Select(entry => entry.Record)),
                group.Key))
            .ToList();
    }

    /// <summary>
    /// Calculates total battery usage (电池使用量) for a set of records.
    /// This sums all drain segments, ignoring charge-ups, matching the
    /// Android system battery usage percentage display.
    /// </summary>
    public static double CalculateDailyBatteryUsed(IEnumerable<BatteryRecord> records)
        => CalculateBatteryUsed(records);

    private static double CalculateBatteryUsed(IEnumerable<BatteryRecord> records)
    {
        var orderedRecords = records
            .OrderBy(record => ConvertUtcSnapshotToSortableValue(record.RecordedAt))
            .ToList();

        if (orderedRecords.Count < 2)
            return 0;

        double totalBatteryUsed = 0;
        for (int i = 1; i < orderedRecords.Count; i++)
        {
            totalBatteryUsed += CalculateDrop(
                orderedRecords[i - 1].BatteryLevel,
                orderedRecords[i].BatteryLevel);
        }

        return Math.Round(totalBatteryUsed, 1);
    }

    private static double CalculateDrop(double previousLevel, double currentLevel)
        => Math.Round(Math.Max(previousLevel - currentLevel, 0), 1);

    private static DateTime ConvertUtcSnapshotToLocal(DateTime recordedAt, TimeZoneInfo timeZone)
    {
        var utcRecordedAt = ConvertSnapshotToUtc(recordedAt);
        return TimeZoneInfo.ConvertTimeFromUtc(utcRecordedAt, timeZone);
    }

    private static DateTime ConvertLocalBoundaryToUtc(DateTime localBoundary, TimeZoneInfo timeZone)
    {
        var normalizedLocalBoundary = localBoundary.Kind switch
        {
            DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(localBoundary, timeZone),
            DateTimeKind.Local => localBoundary,
            _ => DateTime.SpecifyKind(localBoundary, DateTimeKind.Unspecified),
        };

        return TimeZoneInfo.ConvertTimeToUtc(normalizedLocalBoundary, timeZone);
    }

    private static DateTime ConvertSnapshotToUtc(DateTime recordedAt) => recordedAt.Kind switch
    {
        DateTimeKind.Utc => recordedAt,
        DateTimeKind.Local => recordedAt.ToUniversalTime(),
        _ => DateTime.SpecifyKind(recordedAt, DateTimeKind.Utc),
    };

    private static DateTime ConvertUtcSnapshotToSortableValue(DateTime recordedAt)
        => ConvertSnapshotToUtc(recordedAt);
}
