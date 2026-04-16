namespace PowerHunter.Services;

/// <summary>
/// Cross-platform battery monitoring service using the official MAUI Battery API.
/// Records periodic snapshots to SQLite and syncs system usage data on a timer.
/// </summary>
public sealed class BatteryService : IBatteryService, IDisposable
{
    private readonly PowerHunterDatabase _database;
    private readonly DatePartitionStorageService _storage;
    private readonly PowerEstimationService _powerEstimation;
    private readonly BatteryGuardianService _guardianService;
    private readonly AppUsageAlertEvaluator _alertEvaluator;
    private CancellationTokenSource? _monitorCts;
    private double _lastReportedLevel;

    public BatteryService(
        PowerHunterDatabase database,
        DatePartitionStorageService storage,
        PowerEstimationService powerEstimation,
        BatteryGuardianService guardianService,
        AppUsageAlertEvaluator alertEvaluator)
    {
        _database = database;
        _storage = storage;
        _powerEstimation = powerEstimation;
        _guardianService = guardianService;
        _alertEvaluator = alertEvaluator;
        Battery.Default.BatteryInfoChanged += OnBatteryInfoChanged;
    }

    public double CurrentLevel => Battery.Default.ChargeLevel;
    public BatteryState CurrentState => Battery.Default.State;
    public BatteryPowerSource PowerSource => Battery.Default.PowerSource;

    public event EventHandler<double>? BatteryLevelChanged;

    public async Task StartMonitoringAsync(TimeSpan interval)
    {
        StopMonitoring();
        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;

        // Record initial snapshot immediately
        await RecordSnapshotAsync();

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var nextInterval = await ResolveMonitoringIntervalAsync(interval);
                    await Task.Delay(nextInterval, token);
                    await RecordSnapshotAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BatteryService] Monitoring error: {ex}");
                }
            }
        }, token);
    }

    public void StopMonitoring()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;
    }

    public async Task<BatteryRecord> RecordSnapshotAsync()
    {
        var record = new BatteryRecord
        {
            BatteryLevel = Math.Round(CurrentLevel * 100, 1),
            SessionCount = await EstimateSessionCountAsync(),
            ChargingState = CurrentState.ToString(),
            RecordedAt = DateTime.UtcNow,
        };

        await _database.SaveBatteryRecordAsync(record);
        try
        {
            await _storage.PersistBatteryRecordAsync(record);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BatteryService] Date storage sync failed: {ex}");
        }

        // Sync system-reported app usage data alongside the battery snapshot.
        List<AppUsageRecord> usageRecords = [];
        if (_powerEstimation.IsCollectionAvailable)
        {
            try
            {
                usageRecords = await _powerEstimation.CollectAndPersistAsync(DateTime.UtcNow.Date);
            }
            catch
            {
                // Don't let usage collection failure block battery recording
            }
        }

        await _alertEvaluator.EvaluateQuietlyAsync(usageRecords);
        await EvaluateGuardianQuietly(usageRecords, DateTime.UtcNow.Date);

        return record;
    }

    public async Task<List<AppUsageRecord>> GetSystemAppUsageAsync(DateTime since)
    {
        // Try syncing system usage data first.
        if (_powerEstimation.IsCollectionAvailable)
        {
            var real = await _powerEstimation.CollectAndPersistAsync(since);
            if (real.Count > 0) return real;
        }

        // Fallback to whatever is in the database (prior collections)
        return await _database.GetAppUsageAsync(since.Date);
    }

    private async Task EvaluateGuardianQuietly(IEnumerable<AppUsageRecord> usageRecords, DateTime observedSince)
    {
        try
        {
            if (!usageRecords.Any())
                return;

            await _guardianService.EvaluateAsync(usageRecords, observedSince);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BatteryService] Guardian evaluation error: {ex}");
        }
    }

    private void OnBatteryInfoChanged(object? sender, BatteryInfoChangedEventArgs e)
    {
        var level = e.ChargeLevel;
        if (Math.Abs(level - _lastReportedLevel) >= 0.01)
        {
            _lastReportedLevel = level;
            BatteryLevelChanged?.Invoke(this, level);
        }
    }

    private async Task<TimeSpan> ResolveMonitoringIntervalAsync(TimeSpan defaultInterval)
    {
        try
        {
            var settings = await _database.GetSettingsAsync();
            return NightAutoPowerSavingPolicy.ResolveMonitoringInterval(
                settings,
                defaultInterval,
                BatteryRefreshDefaults.NightPowerSavingSnapshotInterval,
                DateTime.Now);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BatteryService] Night power saving interval fallback: {ex}");
            return defaultInterval;
        }
    }

    private async Task<int> EstimateSessionCountAsync()
    {
        var todayRecords = await _database.GetBatteryRecordsAsync(
            DateTime.UtcNow.Date, DateTime.UtcNow);

        if (todayRecords.Count < 2) return 1;

        int sessions = 1;
        for (int i = 1; i < todayRecords.Count; i++)
        {
            var gap = todayRecords[i].RecordedAt - todayRecords[i - 1].RecordedAt;
            if (gap.TotalMinutes > 30) sessions++;
        }

        return sessions;
    }

    public void Dispose()
    {
        StopMonitoring();
        Battery.Default.BatteryInfoChanged -= OnBatteryInfoChanged;
    }
}
