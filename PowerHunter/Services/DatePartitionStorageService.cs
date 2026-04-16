using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace PowerHunter.Services;

/// <summary>
/// Persists daily snapshots into date-based folders and maintains an index
/// that can later be used for fast archival or offline inspection.
/// </summary>
public sealed class DatePartitionStorageService
{
    private const string BatteryRecordsFileName = "battery-records.jsonl";
    private const string AppUsageFileName = "app-usage.json";
    private const string ManifestFileName = "manifest.json";
    private const string IndexFileName = "index.json";

    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly JsonSerializerOptions _prettyJson = new() { WriteIndented = true };
    private readonly JsonSerializerOptions _compactJson = new();
    private readonly string _storageRoot;
    private readonly string _dailyRoot;
    private readonly string _archiveRoot;
    private readonly string _indexPath;

    public DatePartitionStorageService()
    {
        _storageRoot = Path.Combine(FileSystem.AppDataDirectory, "powerhunter-data");
        _dailyRoot = Path.Combine(_storageRoot, "daily");
        _archiveRoot = Path.Combine(_storageRoot, "archive");
        _indexPath = Path.Combine(_storageRoot, IndexFileName);

        Directory.CreateDirectory(_storageRoot);
        Directory.CreateDirectory(_dailyRoot);
        Directory.CreateDirectory(_archiveRoot);
    }

    public string StorageRoot => _storageRoot;

    public string ArchiveRoot => _archiveRoot;

    public async Task PersistBatteryRecordAsync(BatteryRecord record)
    {
        await _syncLock.WaitAsync();
        try
        {
            var dayDirectory = EnsureDayDirectory(record.RecordedAt.Date);
            var batteryFile = Path.Combine(dayDirectory, BatteryRecordsFileName);
            var payload = JsonSerializer.Serialize(record, _compactJson);

            await using var stream = new FileStream(
                batteryFile,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteLineAsync(payload);

            await RefreshIndexAsync(record.RecordedAt.Date);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task PersistAppUsageRecordsAsync(DateTime date, IReadOnlyCollection<AppUsageRecord> records)
    {
        await _syncLock.WaitAsync();
        try
        {
            var dayDirectory = EnsureDayDirectory(date.Date);
            var appUsageFile = Path.Combine(dayDirectory, AppUsageFileName);

            if (records.Count == 0)
            {
                if (File.Exists(appUsageFile))
                {
                    File.Delete(appUsageFile);
                }
            }
            else
            {
                await WriteJsonAsync(appUsageFile, records);
            }

            await RefreshIndexAsync(date.Date);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task MaterializeDayAsync(
        DateTime date,
        IReadOnlyCollection<BatteryRecord> batteryRecords,
        IReadOnlyCollection<AppUsageRecord> appUsageRecords)
    {
        await _syncLock.WaitAsync();
        try
        {
            var dayDirectory = EnsureDayDirectory(date.Date);
            var batteryFile = Path.Combine(dayDirectory, BatteryRecordsFileName);
            var appUsageFile = Path.Combine(dayDirectory, AppUsageFileName);

            if (batteryRecords.Count == 0)
            {
                if (File.Exists(batteryFile))
                {
                    File.Delete(batteryFile);
                }
            }
            else
            {
                await WriteJsonLinesAsync(batteryFile, batteryRecords);
            }

            if (appUsageRecords.Count == 0)
            {
                if (File.Exists(appUsageFile))
                {
                    File.Delete(appUsageFile);
                }
            }
            else
            {
                await WriteJsonAsync(appUsageFile, appUsageRecords);
            }

            await RefreshIndexAsync(date.Date);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<bool> ArchiveDayAsync(DateTime date)
    {
        await _syncLock.WaitAsync();
        try
        {
            var dayDirectory = GetDayDirectory(date.Date);
            if (!Directory.Exists(dayDirectory))
            {
                return false;
            }

            var batteryFile = Path.Combine(dayDirectory, BatteryRecordsFileName);
            var appUsageFile = Path.Combine(dayDirectory, AppUsageFileName);
            var archivePath = GetArchivePath(date.Date);
            Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);

            var entry = await BuildEntryAsync(
                date.Date,
                CountJsonLinesAsync(batteryFile),
                CountAppUsageRecordsAsync(appUsageFile));
            entry.IsArchived = true;
            entry.ArchiveFile = ToRelativePath(archivePath);

            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            ZipFile.CreateFromDirectory(dayDirectory, archivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
            Directory.Delete(dayDirectory, recursive: true);

            var index = await LoadIndexAsync();
            UpsertEntry(index, entry);
            await SaveIndexAsync(index);

            var manifest = new DailyDataManifest
            {
                DateKey = entry.DateKey,
                BatteryRecordsFile = Path.GetFileName(batteryFile),
                AppUsageFile = Path.GetFileName(appUsageFile),
                BatteryRecordCount = entry.BatteryRecordCount,
                AppUsageRecordCount = entry.AppUsageRecordCount,
                LastUpdatedUtc = entry.LastUpdatedUtc,
                IsArchived = true,
                ArchiveFile = entry.ArchiveFile,
            };

            using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);
            var manifestEntry = archive.GetEntry(ManifestFileName);
            manifestEntry?.Delete();
            var createdEntry = archive.CreateEntry(ManifestFileName, CompressionLevel.Optimal);
            await using var manifestStream = createdEntry.Open();
            await JsonSerializer.SerializeAsync(manifestStream, manifest, _prettyJson);

            return true;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task RefreshIndexAsync(DateTime date)
    {
        var dayDirectory = GetDayDirectory(date.Date);
        var batteryFile = Path.Combine(dayDirectory, BatteryRecordsFileName);
        var appUsageFile = Path.Combine(dayDirectory, AppUsageFileName);
        var manifestFile = Path.Combine(dayDirectory, ManifestFileName);
        var archivePath = GetArchivePath(date.Date);

        var entry = await BuildEntryAsync(
            date.Date,
            CountJsonLinesAsync(batteryFile),
            CountAppUsageRecordsAsync(appUsageFile));
        entry.IsArchived = File.Exists(archivePath) && !Directory.Exists(dayDirectory);
        entry.ArchiveFile = entry.IsArchived ? ToRelativePath(archivePath) : null;

        var index = await LoadIndexAsync();
        UpsertEntry(index, entry);
        await SaveIndexAsync(index);

        Directory.CreateDirectory(dayDirectory);
        var manifest = new DailyDataManifest
        {
            DateKey = entry.DateKey,
            BatteryRecordsFile = Path.GetFileName(batteryFile),
            AppUsageFile = Path.GetFileName(appUsageFile),
            BatteryRecordCount = entry.BatteryRecordCount,
            AppUsageRecordCount = entry.AppUsageRecordCount,
            LastUpdatedUtc = entry.LastUpdatedUtc,
            IsArchived = entry.IsArchived,
            ArchiveFile = entry.ArchiveFile,
        };
        await WriteJsonAsync(manifestFile, manifest);
    }

    private async Task<DatePartitionEntry> BuildEntryAsync(
        DateTime date,
        Task<int> batteryCountTask,
        Task<int> appUsageCountTask)
    {
        var dayDirectory = GetDayDirectory(date.Date);
        var batteryFile = Path.Combine(dayDirectory, BatteryRecordsFileName);
        var appUsageFile = Path.Combine(dayDirectory, AppUsageFileName);
        var manifestFile = Path.Combine(dayDirectory, ManifestFileName);

        var batteryCount = await batteryCountTask;
        var appUsageCount = await appUsageCountTask;

        return new DatePartitionEntry
        {
            DateKey = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            RelativeDirectory = ToRelativePath(dayDirectory),
            BatteryRecordsFile = ToRelativePath(batteryFile),
            AppUsageFile = ToRelativePath(appUsageFile),
            ManifestFile = ToRelativePath(manifestFile),
            BatteryRecordCount = batteryCount,
            AppUsageRecordCount = appUsageCount,
            LastUpdatedUtc = DateTime.UtcNow,
        };
    }

    private async Task<DatePartitionIndex> LoadIndexAsync()
    {
        if (!File.Exists(_indexPath))
        {
            return new DatePartitionIndex();
        }

        await using var stream = new FileStream(
            _indexPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: true);

        return await JsonSerializer.DeserializeAsync<DatePartitionIndex>(stream, _prettyJson)
               ?? new DatePartitionIndex();
    }

    private async Task SaveIndexAsync(DatePartitionIndex index)
    {
        index.UpdatedAtUtc = DateTime.UtcNow;
        index.Partitions = index.Partitions
            .OrderBy(p => p.DateKey)
            .ToList();

        await WriteJsonAsync(_indexPath, index);
    }

    private static void UpsertEntry(DatePartitionIndex index, DatePartitionEntry entry)
    {
        var existing = index.Partitions.FirstOrDefault(p => p.DateKey == entry.DateKey);
        if (existing is null)
        {
            index.Partitions.Add(entry);
            return;
        }

        existing.RelativeDirectory = entry.RelativeDirectory;
        existing.BatteryRecordsFile = entry.BatteryRecordsFile;
        existing.AppUsageFile = entry.AppUsageFile;
        existing.ManifestFile = entry.ManifestFile;
        existing.BatteryRecordCount = entry.BatteryRecordCount;
        existing.AppUsageRecordCount = entry.AppUsageRecordCount;
        existing.LastUpdatedUtc = entry.LastUpdatedUtc;
        existing.IsArchived = entry.IsArchived;
        existing.ArchiveFile = entry.ArchiveFile;
    }

    private async Task<int> CountJsonLinesAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        int count = 0;
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (await reader.ReadLineAsync() is not null)
        {
            count++;
        }

        return count;
    }

    private async Task<int> CountAppUsageRecordsAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: true);
        var records = await JsonSerializer.DeserializeAsync<List<AppUsageRecord>>(stream, _prettyJson);
        return records?.Count ?? 0;
    }

    private async Task WriteJsonLinesAsync<T>(string filePath, IEnumerable<T> values)
    {
        var lines = values.Select(value => JsonSerializer.Serialize(value, _compactJson));
        await File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8);
    }

    private async Task WriteJsonAsync<T>(string filePath, T value)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);
        await JsonSerializer.SerializeAsync(stream, value, _prettyJson);
    }

    private string EnsureDayDirectory(DateTime date)
    {
        var dayDirectory = GetDayDirectory(date);
        Directory.CreateDirectory(dayDirectory);
        return dayDirectory;
    }

    private string GetDayDirectory(DateTime date)
        => Path.Combine(
            _dailyRoot,
            date.ToString("yyyy", CultureInfo.InvariantCulture),
            date.ToString("MM", CultureInfo.InvariantCulture),
            date.ToString("dd", CultureInfo.InvariantCulture));

    private string GetArchivePath(DateTime date)
        => Path.Combine(
            _archiveRoot,
            date.ToString("yyyy", CultureInfo.InvariantCulture),
            date.ToString("MM", CultureInfo.InvariantCulture),
            $"{date:yyyy-MM-dd}.zip");

    private string ToRelativePath(string path)
        => Path.GetRelativePath(_storageRoot, path).Replace('\\', '/');
}
