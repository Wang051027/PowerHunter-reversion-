namespace PowerHunter.Services;

/// <summary>
/// Coordinates online SQLite data with the date-partitioned storage archive.
/// </summary>
public sealed class DataLifecycleService
{
    private readonly PowerHunterDatabase _database;
    private readonly DatePartitionStorageService _storage;

    public DataLifecycleService(PowerHunterDatabase database, DatePartitionStorageService storage)
    {
        _database = database;
        _storage = storage;
    }

    public async Task<ArchiveMaintenanceResult> ArchiveHistoricalDataAsync(int keepDetailDays = 30)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-keepDetailDays);
        var batteryRecords = await _database.GetBatteryRecordsBeforeAsync(cutoff);
        var appUsageRecords = await _database.GetAppUsageBeforeAsync(cutoff);

        var result = new ArchiveMaintenanceResult
        {
            DatabaseBytesBefore = _database.GetDatabaseSize(),
            ArchiveRoot = _storage.ArchiveRoot,
        };

        if (batteryRecords.Count == 0 && appUsageRecords.Count == 0)
        {
            result.DatabaseBytesAfter = result.DatabaseBytesBefore;
            return result;
        }

        var batteryByDay = batteryRecords
            .GroupBy(record => record.RecordedAt.Date)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<BatteryRecord>)group.ToList());
        var appUsageByDay = appUsageRecords
            .GroupBy(record => record.Date.Date)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<AppUsageRecord>)group.ToList());

        var days = batteryByDay.Keys
            .Concat(appUsageByDay.Keys)
            .Distinct()
            .OrderBy(day => day)
            .ToList();

        foreach (var day in days)
        {
            var dayBatteryRecords = batteryByDay.GetValueOrDefault(day) ?? [];
            var dayAppUsageRecords = appUsageByDay.GetValueOrDefault(day) ?? [];

            await _storage.MaterializeDayAsync(day, dayBatteryRecords, dayAppUsageRecords);
            if (await _storage.ArchiveDayAsync(day))
            {
                result.ArchivedDayCount++;
                result.BatteryRecordCount += dayBatteryRecords.Count;
                result.AppUsageRecordCount += dayAppUsageRecords.Count;
            }
            else
            {
                throw new InvalidOperationException($"Failed to archive partition for {day:yyyy-MM-dd}.");
            }
        }

        await _database.DeleteRecordsOlderThanAsync(cutoff);

        try
        {
            await _database.VacuumAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataLifecycleService] Vacuum failed: {ex}");
        }

        result.DatabaseBytesAfter = _database.GetDatabaseSize();
        return result;
    }
}
