using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace PowerHunter.ViewModels;

/// <summary>
/// ViewModel for the stats dashboard.
/// The layout is intentionally more editorial, but every data block still maps to
/// the existing Power Hunter telemetry: permission state, live battery level,
/// timeframe switching, top app consumers, battery trend, and category breakdown.
/// </summary>
public partial class StatsViewModel : ObservableObject, IRecipient<PermissionRefreshMessage>
{
    private readonly PowerHunterDatabase _database;
    private readonly IBatteryService _batteryService;
    private readonly PowerEstimationService _powerEstimation;
    private readonly AppUsageAlertEvaluator _alertEvaluator;
    private readonly IAppIconService _appIconService;
    private readonly IUsageStatsPermission _usagePermission;

    private IReadOnlyList<AppUsageRecord> _currentUsageRecords = [];
    private IReadOnlyList<AppUsageRecord> _comparisonUsageRecords = [];

    /// <summary>Well-known package → specific emoji for the app consumer list.</summary>
    private static readonly Dictionary<string, string> AppEmoji = new(StringComparer.OrdinalIgnoreCase)
    {
        ["com.google.android.youtube"] = "\u25b6\ufe0f",
        ["com.zhiliaoapp.musically"] = "\ud83c\udfac",
        ["com.ss.android.ugc.aweme"] = "\ud83c\udfac",
        ["com.ss.android.ugc.trill"] = "\ud83c\udfac",
        ["tv.danmaku.bili"] = "\ud83d\udcfa",
        ["com.bilibili.app.in"] = "\ud83d\udcfa",
        ["com.netflix.mediaclient"] = "\ud83c\udf7f",
        ["tv.twitch.android.app"] = "\ud83d\udfe3",
        ["com.instagram.android"] = "\ud83d\udcf8",
        ["com.facebook.katana"] = "\ud83d\udc4d",
        ["com.twitter.android"] = "\ud83d\udc26",
        ["com.snapchat.android"] = "\ud83d\udc7b",
        ["com.reddit.frontpage"] = "\ud83e\udd16",
        ["com.tencent.mm"] = "\ud83d\udcac",
        ["com.tencent.mobileqq"] = "\ud83d\udc27",
        ["com.sina.weibo"] = "\ud83d\udce2",
        ["com.xiaohongshu.scan"] = "\ud83d\udcd5",
        ["com.whatsapp"] = "\ud83d\udcde",
        ["org.telegram.messenger"] = "\u2708\ufe0f",
        ["com.discord"] = "\ud83c\udfae",
        ["us.zoom.videomeetings"] = "\ud83d\udcf9",
        ["com.microsoft.teams"] = "\ud83d\udcca",
        ["jp.naver.line.android"] = "\ud83d\udcf2",
        ["com.android.chrome"] = "\ud83c\udf10",
        ["org.mozilla.firefox"] = "\ud83e\udd8a",
        ["com.microsoft.emmx"] = "\ud83d\udd35",
        ["com.brave.browser"] = "\ud83e\udde1",
        ["com.spotify.music"] = "\ud83c\udfb6",
        ["com.netease.cloudmusic"] = "\ud83c\udfb5",
        ["com.tencent.qqmusic"] = "\ud83c\udfa4",
        ["com.apple.android.music"] = "\ud83c\udfa7",
        ["com.tencent.ig"] = "\ud83d\udd2b",
        ["com.miHoYo.GenshinImpact"] = "\u2694\ufe0f",
        ["com.supercell.clashofclans"] = "\ud83c\udff0",
        ["com.riotgames.league.wildrift"] = "\ud83c\udfc6",
        ["com.google.android.apps.maps"] = "\ud83d\uddfa\ufe0f",
        ["com.autonavi.minimap"] = "\ud83d\udccd",
        ["com.baidu.BaiduMap"] = "\ud83e\udded",
        ["com.google.android.gm"] = "\ud83d\udce7",
        ["com.microsoft.outlook"] = "\ud83d\udce8",
        ["com.alibaba.android.rimet"] = "\ud83d\udce5",
        ["com.android.vending"] = "\ud83d\udecd\ufe0f",
        ["com.android.settings"] = "\u2699\ufe0f",
    };

    /// <summary>Fallback: display category → emoji when package not in AppEmoji.</summary>
    private static readonly Dictionary<string, string> CategoryEmoji = new(StringComparer.OrdinalIgnoreCase)
    {
        [AppCategoryResolver.Game] = "\ud83c\udfae",
        ["Audio"] = "\ud83c\udfb5",
        ["Music"] = "\ud83c\udfb5",
        [AppCategoryResolver.MusicAudio] = "\ud83c\udfb5",
        [AppCategoryResolver.Video] = "\ud83d\udcf9",
        ["Image"] = "\ud83d\uddbc\ufe0f",
        [AppCategoryResolver.Tools] = "\ud83e\udde0",
        ["Productivity"] = "\ud83d\udcbc",
        ["Accessibility"] = "\u267f\ufe0f",
        ["Maps"] = "\ud83d\uddfa\ufe0f",
        ["Navigation"] = "\ud83e\udded",
        ["News"] = "\ud83d\udcf0",
        [AppCategoryResolver.Social] = "\ud83d\udc65",
        [AppCategoryResolver.Other] = "\ud83d\udcf1",
        ["Gaming"] = "\ud83c\udfae",
        ["Work"] = "\ud83d\udcbc",
        ["Study"] = "\ud83d\udcda",
        ["Entertainment"] = "\ud83c\udfb5",
    };

    /// <summary>Rotating accent colors so each app badge stays visually distinct.</summary>
    private static readonly string[] AppAccentColors =
    [
        "#D7E4EC",
        "#E3F6F2",
        "#E7E9FF",
        "#FFF1DB",
        "#F9E7EF",
        "#E2F4FF",
        "#F6F0CC",
        "#E6F5EA",
    ];

    public StatsViewModel(
        PowerHunterDatabase database,
        IBatteryService batteryService,
        PowerEstimationService powerEstimation,
        AppUsageAlertEvaluator alertEvaluator,
        IAppIconService appIconService,
        IUsageStatsPermission usagePermission)
    {
        _database = database;
        _batteryService = batteryService;
        _powerEstimation = powerEstimation;
        _alertEvaluator = alertEvaluator;
        _appIconService = appIconService;
        _usagePermission = usagePermission;

        WeakReferenceMessenger.Default.Register(this);
        UpdateTimeframeText();
        UpdatePrimaryActionText();
        UpdateSelectionState();
    }

    public void Receive(PermissionRefreshMessage message)
    {
        RefreshPermissionState();
    }

    [ObservableProperty]
    private double _batteryPercent;

    [ObservableProperty]
    private double _batteryProgress;

    [ObservableProperty]
    private int _sessionCount;

    [ObservableProperty]
    private double _todayBatteryUsed;

    [ObservableProperty]
    private string _todayBatteryUsedText = "0%";

    [ObservableProperty]
    private bool _isDayTimeframe = true;

    [ObservableProperty]
    private TrackedAppInfo? _selectedApp;

    [ObservableProperty]
    private ISeries[] _trendSeries = [];

    [ObservableProperty]
    private Axis[] _trendXAxes = [];

    [ObservableProperty]
    private Axis[] _trendYAxes = [];

    [ObservableProperty]
    private ISeries[] _categorySeries = [];

    [ObservableProperty]
    private ObservableCollection<CategoryUsage> _categories = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isUsageStatsSupported;

    [ObservableProperty]
    private bool _isUsageStatsGranted;

    [ObservableProperty]
    private bool _showPermissionBanner;

    [ObservableProperty]
    private ObservableCollection<TrackedAppInfo> _apps = [];

    [ObservableProperty]
    private bool _hasApps;

    [ObservableProperty]
    private string _systemBatteryStatusText = string.Empty;

    [ObservableProperty]
    private string _powerStateText = "Discharging · Battery";

    [ObservableProperty]
    private string _primaryActionText = "Refresh Telemetry";

    [ObservableProperty]
    private string _timeframeDescriptionText = "Today";

    [ObservableProperty]
    private string _trendDescriptionText = "Intraday battery drain from live snapshots";

    [ObservableProperty]
    private string _selectedScopeText = "All Apps";

    [ObservableProperty]
    private bool _hasSelectedApp;

    [ObservableProperty]
    private string _consumerSectionNote = "Tap an app to focus the category breakdown.";

    [ObservableProperty]
    private string _categoryInsightText = "Telemetry will appear here after the first sync.";

    [ObservableProperty]
    private string _telemetryBadgeText = "Awaiting telemetry";

    [ObservableProperty]
    private string _consumerEmptyText = "No app telemetry yet.";

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;

        try
        {
            RefreshPermissionState();
            UpdateTimeframeText();

            await _database.PurgeSeedDataAsync();

            var syncedTodayUsage = await SyncTodayUsageAsync();
            await LoadBatterySummaryAsync();
            await RefreshUsageCollectionsAsync(syncedTodayUsage);
            await LoadTrendChartAsync();
            await LoadCategoryChartAsync();
        }
        finally
        {
            IsLoading = false;
            UpdatePrimaryActionText();
        }
    }

    [RelayCommand]
    private async Task RequestUsageStatsPermissionAsync()
    {
        await _usagePermission.RequestAsync();
        RefreshPermissionState();
    }

    [RelayCommand]
    private async Task RunPrimaryActionAsync()
    {
        if (ShowPermissionBanner)
        {
            await RequestUsageStatsPermissionAsync();
            return;
        }

        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task SetTimeframeAsync(string timeframe)
    {
        var useDayTimeframe = string.Equals(timeframe, "day", StringComparison.OrdinalIgnoreCase);
        if (IsDayTimeframe == useDayTimeframe && TrendSeries.Length > 0)
            return;

        IsDayTimeframe = useDayTimeframe;
        UpdateTimeframeText();

        await RefreshUsageCollectionsAsync();
        await LoadTrendChartAsync();
        await LoadCategoryChartAsync();
    }

    [RelayCommand]
    private async Task SelectAppAsync(TrackedAppInfo? app)
    {
        if (app is not null && SelectedApp?.Id == app.Id)
        {
            SelectedApp = null;
        }
        else
        {
            SelectedApp = app;
        }

        UpdateSelectionState();
        await LoadCategoryChartAsync();
    }

    [RelayCommand]
    private async Task ResetAppSelectionAsync()
    {
        if (SelectedApp is null)
            return;

        SelectedApp = null;
        UpdateSelectionState();
        await LoadCategoryChartAsync();
    }

    private void RefreshPermissionState()
    {
        IsUsageStatsSupported = _usagePermission.IsSupported;
        IsUsageStatsGranted = _usagePermission.IsGranted;
        ShowPermissionBanner = IsUsageStatsSupported && !IsUsageStatsGranted;
        UpdatePrimaryActionText();
    }

    private async Task<List<AppUsageRecord>> SyncTodayUsageAsync()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var todayUsage = _powerEstimation.IsCollectionAvailable
            ? await _powerEstimation.CollectAndPersistAsync(todayUtc)
            : await _database.GetAppUsageAsync(todayUtc);

        await _alertEvaluator.EvaluateQuietlyAsync(todayUsage);
        return todayUsage;
    }

    private async Task LoadBatterySummaryAsync()
    {
        var latest = await _database.GetLatestBatteryRecordAsync();

        BatteryPercent = Math.Round(_batteryService.CurrentLevel * 100d, 1);
        BatteryProgress = Math.Clamp(BatteryPercent / 100d, 0d, 1d);
        SessionCount = latest?.SessionCount ?? 1;
        PowerStateText = BuildPowerStateText();
        SystemBatteryStatusText = latest is null
            ? $"Official system battery data · auto refresh every {BatteryRefreshDefaults.UiRefreshInterval.TotalMinutes:0} min"
            : $"Official system battery data · last synced {latest.RecordedAt.ToLocalTime():HH:mm}";

        var localToday = DateTime.Now.Date;
        var localTimeZone = TimeZoneInfo.Local;
        var todayBounds = BatteryTrendBuilder.GetUtcBoundsForLocalDate(localToday, localTimeZone);
        var todayRecords = await _database.GetBatteryRecordsAsync(todayBounds.FromUtc, todayBounds.ToUtc);

        TodayBatteryUsed = BatteryTrendBuilder.CalculateDailyBatteryUsed(todayRecords);
        TodayBatteryUsedText = $"{TodayBatteryUsed:0.#}%";
    }

    private async Task RefreshUsageCollectionsAsync(IReadOnlyList<AppUsageRecord>? syncedTodayUsage = null)
    {
        var usageWindow = GetUsageWindow();

        _currentUsageRecords = IsDayTimeframe && syncedTodayUsage is not null
            ? syncedTodayUsage
            : await _database.GetAppUsageRangeAsync(usageWindow.CurrentFrom, usageWindow.CurrentTo);

        _comparisonUsageRecords = await _database.GetAppUsageRangeAsync(
            usageWindow.ComparisonFrom,
            usageWindow.ComparisonTo);

        await LoadAppsFromUsageAsync(_currentUsageRecords, _comparisonUsageRecords);

        TelemetryBadgeText = BuildTelemetryBadgeText(_currentUsageRecords);
        ConsumerEmptyText = BuildConsumerEmptyText();
    }

    private async Task LoadAppsFromUsageAsync(
        IReadOnlyList<AppUsageRecord> currentRecords,
        IReadOnlyList<AppUsageRecord> comparisonRecords)
    {
        var previousSelectedId = SelectedApp?.Id;
        var currentShares = CalculateNormalizedShares(currentRecords);
        var comparisonShares = CalculateNormalizedShares(comparisonRecords);

        var groupedRecords = currentRecords
            .GroupBy(record => record.AppId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var latestRecord = group
                    .OrderByDescending(record => record.Date)
                    .ThenByDescending(record => record.LastSyncedAtUtc ?? DateTime.MinValue)
                    .First();

                var iconSource = _appIconService.GetIcon(group.Key);
                var category = AppCategoryResolver.GetPreferredCategoryLabel(latestRecord);
                var fallbackGlyph = AppEmoji.GetValueOrDefault(group.Key)
                    ?? CategoryEmoji.GetValueOrDefault(category, "\ud83d\udcf1");
                var colorIndex = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(group.Key)) % AppAccentColors.Length;
                var accentColor = AppAccentColors[colorIndex];
                var badgeColor = iconSource is null
                    ? Color.FromArgb(AppCategoryResolver.GetColor(category))
                    : Color.FromArgb(accentColor);

                return new TrackedAppInfo(
                    group.Key,
                    BuildDisplayName(latestRecord.AppName),
                    fallbackGlyph,
                    badgeColor,
                    iconSource is null ? Colors.White : Color.FromArgb("#191C1D"),
                    iconSource,
                    currentShares.GetValueOrDefault(group.Key),
                    currentShares.GetValueOrDefault(group.Key) - comparisonShares.GetValueOrDefault(group.Key),
                    category,
                    latestRecord.IsOfficialPowerData);
            })
            .OrderByDescending(app => app.UsagePercentage)
            .ToList();

        Apps = new ObservableCollection<TrackedAppInfo>(groupedRecords);
        HasApps = groupedRecords.Count > 0;

        SelectedApp = groupedRecords.FirstOrDefault(app =>
            string.Equals(app.Id, previousSelectedId, StringComparison.OrdinalIgnoreCase));

        UpdateSelectionState();
        await Task.CompletedTask;
    }

    private async Task LoadTrendChartAsync()
    {
        var localToday = DateTime.Now.Date;
        var localTimeZone = TimeZoneInfo.Local;
        List<TrendPoint> trendPoints;

        if (IsDayTimeframe)
        {
            var todayBounds = BatteryTrendBuilder.GetUtcBoundsForLocalDate(localToday, localTimeZone);
            var intradayRecords = await _database.GetBatteryRecordsAsync(todayBounds.FromUtc, todayBounds.ToUtc);
            trendPoints = BatteryTrendBuilder.BuildIntradayUsageTrend(intradayRecords, localToday, localTimeZone);

            if (trendPoints.Count == 0)
            {
                trendPoints =
                [
                    new TrendPoint("Now", 0, DateTime.Now)
                ];
            }
        }
        else
        {
            var rangeStartLocal = localToday.AddDays(-6);
            var rangeStartBounds = BatteryTrendBuilder.GetUtcBoundsForLocalDate(rangeStartLocal, localTimeZone);
            var rangeEndUtc = BatteryTrendBuilder.GetUtcBoundsForLocalDate(localToday.AddDays(1), localTimeZone).FromUtc;
            var records = await _database.GetBatteryRecordsAsync(rangeStartBounds.FromUtc, rangeEndUtc);

            trendPoints = BatteryTrendBuilder.BuildDailyUsageTrend(
                records,
                rangeStartLocal,
                localToday,
                localTimeZone,
                todayLocalDateOverride: localToday);

            if (trendPoints.Count == 0)
            {
                trendPoints =
                [
                    new TrendPoint("Today", 0, localToday)
                ];
            }
        }

        var values = trendPoints.Select(point => new ObservableValue(point.Value)).ToArray();
        var labels = BuildSparseTrendLabels(trendPoints);
        var maxVal = trendPoints.Max(point => point.Value);
        var yMax = Math.Max(maxVal + 10, 20);

        TrendSeries =
        [
            new LineSeries<ObservableValue>
            {
                Values = values,
                Stroke = new SolidColorPaint(SKColor.Parse("#006B54"), 3),
                Fill = new SolidColorPaint(SKColor.Parse("#24FFCD").WithAlpha(35)),
                GeometryFill = new SolidColorPaint(SKColor.Parse("#24FFCD")),
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#F8FAFA"), 2),
                GeometrySize = 9,
                LineSmoothness = 0.55,
            }
        ];

        TrendXAxes =
        [
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#546067")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E6E8E8")) { StrokeThickness = 1 },
                TextSize = 10,
                LabelsRotation = IsDayTimeframe ? 0 : 22,
            }
        ];

        TrendYAxes =
        [
            new Axis
            {
                Name = "Battery Used (%)",
                NamePaint = new SolidColorPaint(SKColor.Parse("#546067")),
                NameTextSize = 11,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#546067")),
                Labeler = value => $"{value:0}%",
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E6E8E8")) { StrokeThickness = 1 },
                TextSize = 11,
                MinLimit = 0,
                MaxLimit = yMax,
            }
        ];
    }

    private async Task LoadCategoryChartAsync()
    {
        List<CategoryUsage> categories;

        if (_currentUsageRecords.Count == 0)
        {
            categories =
            [
                new CategoryUsage("No Data", 100, "#E1E3E3"),
            ];

            CategoryInsightText = BuildNoDataInsight();
        }
        else if (SelectedApp is not null)
        {
            var selectedShare = CalculateNormalizedShares(_currentUsageRecords)
                .GetValueOrDefault(SelectedApp.Id);
            var otherShare = Math.Round(Math.Max(100d - selectedShare, 0d), 1);
            var categoryLabel = SelectedApp.CategoryLabel;

            categories =
            [
                new CategoryUsage(categoryLabel, Math.Round(selectedShare, 1), AppCategoryResolver.GetColor(categoryLabel)),
                new CategoryUsage("Other Apps", otherShare, "#E1E3E3"),
            ];

            CategoryInsightText =
                $"{SelectedApp.Name} represents {selectedShare:0.#}% of the {TimeframeDescriptionText.ToLowerInvariant()} telemetry window. Compare it against the rest of the system to spot runaway sessions.";
        }
        else
        {
            categories = BuildCategoryDistribution(_currentUsageRecords);
            var leadCategory = categories.OrderByDescending(category => category.Percentage).FirstOrDefault();
            CategoryInsightText = leadCategory is null
                ? BuildNoDataInsight()
                : $"{leadCategory.Name} leads the {TimeframeDescriptionText.ToLowerInvariant()} load at {leadCategory.Percentage:0.#}% of observed usage.";
        }

        Categories = new ObservableCollection<CategoryUsage>(categories);

        CategorySeries = categories.Select(category => new PieSeries<double>
        {
            Values = [category.Percentage],
            Name = category.Name,
            Fill = new SolidColorPaint(SKColor.Parse(category.HexColor)),
            InnerRadius = 56,
            Pushout = 0,
            HoverPushout = 0,
        } as ISeries).ToArray();

        await Task.CompletedTask;
    }

    private void UpdateTimeframeText()
    {
        TimeframeDescriptionText = IsDayTimeframe ? "Today" : "Last 7 Days";
        TrendDescriptionText = IsDayTimeframe
            ? "Intraday battery drain from live snapshots"
            : "Daily battery drain across the last seven days";
    }

    private void UpdateSelectionState()
    {
        foreach (var app in Apps)
        {
            app.IsSelected = SelectedApp is not null &&
                             string.Equals(app.Id, SelectedApp.Id, StringComparison.OrdinalIgnoreCase);
        }

        HasSelectedApp = SelectedApp is not null;
        SelectedScopeText = SelectedApp?.Name ?? "All Apps";
        ConsumerSectionNote = SelectedApp is null
            ? $"Showing the highest app load for {TimeframeDescriptionText.ToLowerInvariant()}."
            : $"Focused on {SelectedApp.Name}. Tap it again or choose All Apps to clear the filter.";
    }

    private void UpdatePrimaryActionText()
    {
        PrimaryActionText = ShowPermissionBanner
            ? "Grant Usage Access"
            : IsLoading
                ? "Syncing Telemetry..."
                : "Refresh Telemetry";
    }

    private string BuildPowerStateText()
    {
        var stateText = _batteryService.CurrentState switch
        {
            BatteryState.Charging => "Charging",
            BatteryState.Full => "Fully Charged",
            BatteryState.NotCharging => "Idle",
            BatteryState.Unknown => "Battery State Unknown",
            _ => "Discharging",
        };

        var sourceText = _batteryService.PowerSource switch
        {
            BatteryPowerSource.AC => "AC Power",
            BatteryPowerSource.Usb => "USB",
            BatteryPowerSource.Wireless => "Wireless",
            BatteryPowerSource.Unknown => "Unknown Source",
            _ => "Battery",
        };

        return $"{stateText} · {sourceText}";
    }

    private string BuildTelemetryBadgeText(IReadOnlyList<AppUsageRecord> records)
    {
        if (records.Count == 0)
        {
            return _powerEstimation.IsCollectionAvailable
                ? "Awaiting telemetry"
                : "Telemetry unavailable";
        }

        return records.Any(record => record.IsOfficialPowerData)
            ? "Official power telemetry"
            : "Usage-share telemetry";
    }

    private string BuildConsumerEmptyText()
    {
        if (ShowPermissionBanner)
            return "Grant Usage Access to surface the highest-drain apps.";

        if (!_powerEstimation.IsCollectionAvailable)
            return "Per-app power telemetry is unavailable on this platform.";

        return IsDayTimeframe
            ? "Waiting for the first app sync of the day."
            : "No weekly app telemetry has been recorded yet.";
    }

    private string BuildNoDataInsight()
    {
        if (ShowPermissionBanner)
            return "Grant Usage Access to unlock category-level battery attribution.";

        if (!_powerEstimation.IsCollectionAvailable)
            return "This platform does not expose app-level power attribution, so the category chart stays quiet.";

        return $"Power Hunter is waiting for enough {TimeframeDescriptionText.ToLowerInvariant()} telemetry to build the category breakdown.";
    }

    private static Dictionary<string, double> CalculateNormalizedShares(IEnumerable<AppUsageRecord> records)
    {
        var materializedRecords = records.ToList();
        var totalUsage = materializedRecords.Sum(record => record.UsagePercentage);
        if (totalUsage <= 0)
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        return materializedRecords
            .GroupBy(record => record.AppId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => Math.Round(group.Sum(record => record.UsagePercentage) / totalUsage * 100d, 1),
                StringComparer.OrdinalIgnoreCase);
    }

    private static List<CategoryUsage> BuildCategoryDistribution(IEnumerable<AppUsageRecord> records)
    {
        var materializedRecords = records.ToList();
        var totalUsage = materializedRecords.Sum(record => record.UsagePercentage);
        if (totalUsage <= 0)
            return [];

        return materializedRecords
            .GroupBy(AppCategoryResolver.GetPreferredCategoryLabel)
            .Select(group =>
            {
                var percentage = Math.Round(group.Sum(record => record.UsagePercentage) / totalUsage * 100d, 1);
                return new CategoryUsage(
                    group.Key,
                    percentage,
                    AppCategoryResolver.GetColor(group.Key));
            })
            .OrderByDescending(category => category.Percentage)
            .ToList();
    }

    private static string BuildDisplayName(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            return "Unknown App";

        const int maxLength = 22;
        return appName.Length <= maxLength
            ? appName
            : appName[..(maxLength - 3)] + "...";
    }

    private static string[] BuildSparseTrendLabels(IReadOnlyList<TrendPoint> trendPoints)
    {
        if (trendPoints.Count == 0)
            return [];

        var maxVisibleLabels = trendPoints.Count <= 6 ? trendPoints.Count : 6;
        var step = Math.Max((int)Math.Ceiling(trendPoints.Count / (double)maxVisibleLabels), 1);

        var labels = new string[trendPoints.Count];
        for (int i = 0; i < trendPoints.Count; i++)
        {
            labels[i] = (i % step == 0 || i == trendPoints.Count - 1)
                ? trendPoints[i].Label
                : string.Empty;
        }

        return labels;
    }

    private UsageWindow GetUsageWindow()
    {
        var todayUtc = DateTime.UtcNow.Date;

        return IsDayTimeframe
            ? new UsageWindow(
                todayUtc,
                DateTime.UtcNow,
                todayUtc.AddDays(-1),
                todayUtc.AddDays(-1))
            : new UsageWindow(
                todayUtc.AddDays(-6),
                DateTime.UtcNow,
                todayUtc.AddDays(-13),
                todayUtc.AddDays(-7));
    }

    private sealed record UsageWindow(
        DateTime CurrentFrom,
        DateTime CurrentTo,
        DateTime ComparisonFrom,
        DateTime ComparisonTo);
}
