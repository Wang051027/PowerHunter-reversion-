using System.Globalization;
using SQLite;

namespace PowerHunter.Data;

/// <summary>
/// SQLite database for all Power Hunter local data.
/// Handles table creation, CRUD operations, indexing, and historical data archiving.
/// All data stays on-device — nothing is uploaded to any server.
/// </summary>
public sealed class PowerHunterDatabase
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;

    public PowerHunterDatabase()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "powerhunter.db3");
    }

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_database is not null)
            return _database;

        _database = new SQLiteAsyncConnection(_dbPath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache);

        await _database.CreateTableAsync<BatteryRecord>();
        await _database.CreateTableAsync<AppUsageRecord>();
        await _database.CreateTableAsync<BatteryAlert>();
        await _database.CreateTableAsync<BackgroundDrainEvent>();
        await _database.CreateTableAsync<UserSettings>();
        await EnsureAppUsageSchemaAsync(_database);
        await EnsureBackgroundDrainSchemaAsync(_database);
        await EnsureUserSettingsSchemaAsync(_database);
        await _database.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_BatteryRecords_RecordedAt ON BatteryRecords(RecordedAt)");
        await _database.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_AppUsageRecords_Date_AppId ON AppUsageRecords(Date, AppId)");
        await _database.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_AppUsageRecords_Date_Category ON AppUsageRecords(Date, Category)");
        await _database.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_BackgroundDrainEvents_AppId_DetectedAt ON BackgroundDrainEvents(AppId, DetectedAt)");

        // Ensure default settings row exists
        var settings = await _database.Table<UserSettings>().FirstOrDefaultAsync();
        if (settings is null)
        {
            await _database.InsertAsync(new UserSettings());
        }

        return _database;
    }

    // ──────────────────────────────────────────────
    // BatteryRecord
    // ──────────────────────────────────────────────

    public async Task<int> SaveBatteryRecordAsync(BatteryRecord record)
    {
        var db = await GetConnectionAsync();
        return await db.InsertAsync(record);
    }

    public async Task<List<BatteryRecord>> GetBatteryRecordsAsync(DateTime from, DateTime to)
    {
        var db = await GetConnectionAsync();
        return await db.Table<BatteryRecord>()
            .Where(r => r.RecordedAt >= from && r.RecordedAt <= to)
            .OrderBy(r => r.RecordedAt)
            .ToListAsync();
    }

    public async Task<BatteryRecord?> GetLatestBatteryRecordAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<BatteryRecord>()
            .OrderByDescending(r => r.RecordedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<BatteryRecord>> GetBatteryRecordsBeforeAsync(DateTime cutoff)
    {
        var db = await GetConnectionAsync();
        return await db.Table<BatteryRecord>()
            .Where(r => r.RecordedAt < cutoff)
            .OrderBy(r => r.RecordedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Returns daily average battery levels for the last N days (for trend chart).
    /// </summary>
    public async Task<List<TrendPoint>> GetDailyTrendAsync(int days = 7)
    {
        var db = await GetConnectionAsync();
        var since = DateTime.UtcNow.Date.AddDays(-days + 1);
        var records = await db.Table<BatteryRecord>()
            .Where(r => r.RecordedAt >= since)
            .ToListAsync();

        return records
            .GroupBy(r => r.RecordedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new TrendPoint(
                Label: g.Key == DateTime.UtcNow.Date
                    ? "Today"
                    : g.Key.ToString("M/d ddd", CultureInfo.InvariantCulture),
                Value: Math.Round(g.Average(r => r.BatteryLevel), 1),
                Date: g.Key
            ))
            .ToList();
    }

    // ──────────────────────────────────────────────
    // AppUsageRecord
    // ──────────────────────────────────────────────

    public async Task<int> SaveAppUsageAsync(AppUsageRecord record)
    {
        var db = await GetConnectionAsync();
        return await db.InsertAsync(record);
    }

    public async Task SaveAppUsageBatchAsync(IEnumerable<AppUsageRecord> records)
    {
        var db = await GetConnectionAsync();
        await db.InsertAllAsync(records);
    }

    /// <summary>
    /// Replaces all app usage records for a given date (upsert pattern).
    /// Prevents duplicate entries when re-collecting data for the same day.
    /// </summary>
    public async Task ReplaceAppUsageForDateAsync(DateTime date, IEnumerable<AppUsageRecord> records)
    {
        var db = await GetConnectionAsync();
        await db.ExecuteAsync("DELETE FROM AppUsageRecords WHERE Date = ?", date.Date);
        await db.InsertAllAsync(records);
    }

    public async Task<List<AppUsageRecord>> GetAppUsageAsync(DateTime date)
    {
        var db = await GetConnectionAsync();
        return await db.Table<AppUsageRecord>()
            .Where(r => r.Date == date.Date)
            .OrderByDescending(r => r.UsagePercentage)
            .ToListAsync();
    }

    public async Task<List<AppUsageRecord>> GetAppUsageRangeAsync(DateTime from, DateTime to)
    {
        var db = await GetConnectionAsync();
        return await db.Table<AppUsageRecord>()
            .Where(r => r.Date >= from.Date && r.Date <= to.Date)
            .ToListAsync();
    }

    public async Task<List<AppUsageRecord>> GetAppUsageBeforeAsync(DateTime cutoff)
    {
        var db = await GetConnectionAsync();
        return await db.Table<AppUsageRecord>()
            .Where(r => r.Date < cutoff.Date)
            .OrderBy(r => r.Date)
            .ToListAsync();
    }

    /// <summary>
    /// Aggregates usage by category for the given date range.
    /// </summary>
    public async Task<List<CategoryUsage>> GetCategoryDistributionAsync(DateTime from, DateTime to)
    {
        var records = await GetAppUsageRangeAsync(from, to);

        var totalUsage = records.Sum(r => r.UsagePercentage);
        if (totalUsage <= 0) return [];

        return records
            .GroupBy(AppCategoryResolver.GetPreferredCategoryLabel)
            .Select(g =>
            {
                var pct = Math.Round(g.Sum(r => r.UsagePercentage) / totalUsage * 100, 1);
                // Use AppCategoryResolver for authoritative per-category colors
                var color = AppCategoryResolver.GetColor(g.Key);
                return new CategoryUsage(g.Key, pct, color);
            })
            .OrderByDescending(c => c.Percentage)
            .ToList();
    }

    // ──────────────────────────────────────────────
    // BatteryAlert
    // ──────────────────────────────────────────────

    public async Task<int> SaveAlertAsync(BatteryAlert alert)
    {
        var db = await GetConnectionAsync();
        return alert.Id == 0
            ? await db.InsertAsync(alert)
            : await db.UpdateAsync(alert);
    }

    public async Task<List<BatteryAlert>> GetAlertsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<BatteryAlert>()
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> DeleteAlertAsync(int alertId)
    {
        var db = await GetConnectionAsync();
        return await db.DeleteAsync<BatteryAlert>(alertId);
    }

    // ──────────────────────────────────────────────
    // BackgroundDrainEvent
    // ──────────────────────────────────────────────

    public async Task<int> SaveBackgroundDrainEventAsync(BackgroundDrainEvent drainEvent)
    {
        var db = await GetConnectionAsync();
        return drainEvent.Id == 0
            ? await db.InsertAsync(drainEvent)
            : await db.UpdateAsync(drainEvent);
    }

    public async Task<List<BackgroundDrainEvent>> GetRecentBackgroundDrainEventsAsync(int limit = 5)
    {
        var db = await GetConnectionAsync();
        return await db.QueryAsync<BackgroundDrainEvent>(
            "SELECT * FROM BackgroundDrainEvents ORDER BY DetectedAt DESC LIMIT ?",
            limit);
    }

    public async Task<BackgroundDrainEvent?> GetLatestBackgroundDrainEventAsync(string appId)
    {
        var db = await GetConnectionAsync();
        return await db.FindWithQueryAsync<BackgroundDrainEvent>(
            "SELECT * FROM BackgroundDrainEvents WHERE AppId = ? ORDER BY DetectedAt DESC LIMIT 1",
            appId);
    }

    // ──────────────────────────────────────────────
    // UserSettings (single-row pattern)
    // ──────────────────────────────────────────────

    public async Task<UserSettings> GetSettingsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<UserSettings>().FirstOrDefaultAsync()
               ?? new UserSettings();
    }

    public async Task SaveSettingsAsync(UserSettings settings)
    {
        var db = await GetConnectionAsync();
        settings.Id = 1;
        await db.InsertOrReplaceAsync(settings);
    }

    // ──────────────────────────────────────────────
    // Data Maintenance
    // ──────────────────────────────────────────────

    /// <summary>
    /// Archives old battery records by deleting granular data older than the cutoff
    /// and keeping only daily summaries. Call periodically to prevent unbounded growth.
    /// </summary>
    public async Task ArchiveOldRecordsAsync(int keepDetailDays = 30)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-keepDetailDays);
        await DeleteRecordsOlderThanAsync(cutoff);
        await VacuumAsync();
    }

    public async Task<int> DeleteRecordsOlderThanAsync(DateTime cutoff)
    {
        var db = await GetConnectionAsync();

        var deletedBatteryRecords = await db.ExecuteAsync(
            "DELETE FROM BatteryRecords WHERE RecordedAt < ?",
            cutoff);
        var deletedAppUsageRecords = await db.ExecuteAsync(
            "DELETE FROM AppUsageRecords WHERE Date < ?",
            cutoff.Date);
        var deletedGuardianEvents = await db.ExecuteAsync(
            "DELETE FROM BackgroundDrainEvents WHERE DetectedAt < ?",
            cutoff);

        return deletedBatteryRecords + deletedAppUsageRecords + deletedGuardianEvents;
    }

    /// <summary>
    /// Removes fake seed data — AppIds without a dot are not real package names.
    /// Safe to call on every startup.
    /// </summary>
    public async Task PurgeSeedDataAsync()
    {
        var db = await GetConnectionAsync();
        // Real Android package names always contain dots (e.g. "com.google.android.youtube")
        // Seed data used simple IDs like "youtube", "pubg", "tiktok"
        await db.ExecuteAsync(
            "DELETE FROM AppUsageRecords WHERE AppId NOT LIKE '%.%'");
    }

    /// <summary>
    /// Returns approximate database size in bytes.
    /// </summary>
    public long GetDatabaseSize()
    {
        var fileInfo = new FileInfo(_dbPath);
        return fileInfo.Exists ? fileInfo.Length : 0;
    }

    public async Task VacuumAsync()
    {
        var db = await GetConnectionAsync();
        await db.ExecuteAsync("VACUUM");
    }

    private static async Task EnsureAppUsageSchemaAsync(SQLiteAsyncConnection database)
    {
        await EnsureColumnAsync(database, "AppUsageRecords", "BackgroundUsageMinutes", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnAsync(database, "AppUsageRecords", "ForegroundServiceMinutes", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnAsync(database, "AppUsageRecords", "PowerConsumedMah", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnAsync(database, "AppUsageRecords", "UsageSource", "TEXT NOT NULL DEFAULT 'system-usage-stats'");
        await EnsureColumnAsync(database, "AppUsageRecords", "IsOfficialPowerData", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(database, "AppUsageRecords", "LastSyncedAtUtc", "TEXT NULL");
        await EnsureColumnAsync(database, "AppUsageRecords", "OriginalCategory", "TEXT NOT NULL DEFAULT ''");
    }

    private static async Task EnsureBackgroundDrainSchemaAsync(SQLiteAsyncConnection database)
    {
        await EnsureColumnAsync(database, "BackgroundDrainEvents", "UsageSource", "TEXT NOT NULL DEFAULT 'system-usage-stats'");
        await EnsureColumnAsync(database, "BackgroundDrainEvents", "IsOfficialPowerData", "INTEGER NOT NULL DEFAULT 0");
    }

    private static async Task EnsureUserSettingsSchemaAsync(SQLiteAsyncConnection database)
    {
        await EnsureColumnAsync(database, "UserSettings", "DarkModeEnabled", "INTEGER NOT NULL DEFAULT 1");
        await EnsureColumnAsync(database, "UserSettings", "ThemePreferenceConfigured", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(database, "UserSettings", "NightAutoPowerSavingEnabled", "INTEGER NOT NULL DEFAULT 0");
    }

    private static async Task EnsureColumnAsync(
        SQLiteAsyncConnection database,
        string tableName,
        string columnName,
        string definition)
    {
        var columns = await database.QueryAsync<TableInfoRow>($"PRAGMA table_info({tableName})");
        if (columns.Any(column => string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase)))
            return;

        await database.ExecuteAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}");
    }

    private sealed class TableInfoRow
    {
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }
}
