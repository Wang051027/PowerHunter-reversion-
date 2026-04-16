namespace PowerHunter.ViewModels;

/// <summary>
/// ViewModel for the Apps page — shows system-reported per-app foreground usage
/// from Android UsageStatsManager. Each app shows foreground time, usage share,
/// and category.
/// </summary>
public partial class AppsViewModel : ObservableObject
{
    private readonly PowerHunterDatabase _database;
    private readonly PowerEstimationService _powerEstimation;
    private readonly AppUsageAlertEvaluator _alertEvaluator;
    private readonly IAppUsageCollector _collector;
    private readonly IUsageStatsPermission _usagePermission;

    public AppsViewModel(
        PowerHunterDatabase database,
        PowerEstimationService powerEstimation,
        AppUsageAlertEvaluator alertEvaluator,
        IAppUsageCollector collector,
        IUsageStatsPermission usagePermission)
    {
        _database = database;
        _powerEstimation = powerEstimation;
        _alertEvaluator = alertEvaluator;
        _collector = collector;
        _usagePermission = usagePermission;
    }

    [ObservableProperty]
    private ObservableCollection<AppUsageDisplay> _apps = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _showPermissionBanner;

    [ObservableProperty]
    private int _totalAppsDetected;

    [ObservableProperty]
    private string _totalForegroundTime = "0m";

    [ObservableProperty]
    private double _topUsageSharePercent;

    [ObservableProperty]
    private string _dataSourceStatusText = string.Empty;

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            ShowPermissionBanner = _usagePermission.IsSupported && !_usagePermission.IsGranted;

            List<AppUsageRecord> records;

            if (_powerEstimation.IsCollectionAvailable)
            {
                records = await _powerEstimation.CollectAndPersistAsync(DateTime.UtcNow.Date);
            }
            else
            {
                records = await _database.GetAppUsageAsync(DateTime.UtcNow.Date);
            }

            await _alertEvaluator.EvaluateQuietlyAsync(records);
            ApplyRecords(records);
            DataSourceStatusText = BuildDataSourceStatusText(records, includeTimestamp: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppsVM] LoadData failed: {ex}");
            DataSourceStatusText = "Unable to refresh system usage stats";
            IsEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            ShowPermissionBanner = _usagePermission.IsSupported && !_usagePermission.IsGranted;

            var records = _powerEstimation.IsCollectionAvailable
                ? await _powerEstimation.ForceCollectAsync(DateTime.UtcNow.Date)
                : await _database.GetAppUsageAsync(DateTime.UtcNow.Date);

            await _alertEvaluator.EvaluateQuietlyAsync(records);
            ApplyRecords(records);
            DataSourceStatusText = BuildDataSourceStatusText(records, includeTimestamp: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppsVM] Refresh failed: {ex}");
            DataSourceStatusText = "Unable to refresh system usage stats";
            IsEmpty = Apps.Count == 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RequestPermissionAsync()
    {
        await _usagePermission.RequestAsync();
    }

    private void ApplyRecords(List<AppUsageRecord> records)
    {
        var displayItems = records
            .OrderByDescending(r => r.UsagePercentage)
            .Select(r =>
            {
                var category = AppCategoryResolver.GetPreferredCategoryLabel(r);
                return new AppUsageDisplay
                {
                    AppName = r.AppName,
                    PackageName = r.AppId,
                    Category = category,
                    CategoryColor = Color.FromArgb(AppCategoryResolver.GetColor(category)),
                    UsageSharePercent = r.UsagePercentage,
                    UsageMinutes = r.UsageMinutes,
                    FormattedTime = FormatMinutes(r.UsageMinutes),
                    BarWidth = Math.Max(r.UsagePercentage * 2, 8),
                };
            })
            .ToList();

        Apps = new ObservableCollection<AppUsageDisplay>(displayItems);
        TotalAppsDetected = displayItems.Count;
        TotalForegroundTime = FormatMinutes(displayItems.Sum(a => a.UsageMinutes));
        TopUsageSharePercent = displayItems.Count == 0
            ? 0
            : Math.Round(displayItems.Max(a => a.UsageSharePercent), 1);
        IsEmpty = displayItems.Count == 0;
    }

    private string BuildDataSourceStatusText(IReadOnlyList<AppUsageRecord> records, bool includeTimestamp)
    {
        if (!_collector.IsAvailable)
            return "Waiting for system usage permission";

        var refreshSuffix = includeTimestamp
            ? $"refreshed {(records.Max(record => record.LastSyncedAtUtc) ?? DateTime.UtcNow).ToLocalTime():HH:mm}"
            : $"auto refresh every {BatteryRefreshDefaults.UiRefreshInterval.TotalMinutes:0} min";

        if (records.Count == 0)
            return $"System usage stats · {refreshSuffix}";

        return AppUsageSourceKind.IsOfficial(records[0].UsageSource)
            ? $"Official system power stats · {refreshSuffix}"
            : $"System usage stats fallback · {refreshSuffix}";
    }

    private static string FormatMinutes(double minutes)
    {
        if (minutes < 1) return "<1m";
        if (minutes < 60) return $"{(int)minutes}m";
        var hours = (int)(minutes / 60);
        var mins = (int)(minutes % 60);
        return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
    }
}

/// <summary>
/// Display model for a single app's usage in the Apps list.
/// </summary>
public class AppUsageDisplay
{
    public string AppName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Color CategoryColor { get; set; } = Colors.Gray;
    public double UsageSharePercent { get; set; }
    public double UsageMinutes { get; set; }
    public string FormattedTime { get; set; } = "0m";
    public double BarWidth { get; set; }
}
