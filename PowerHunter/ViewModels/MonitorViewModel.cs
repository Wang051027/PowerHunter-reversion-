namespace PowerHunter.ViewModels;

/// <summary>
/// ViewModel for the Monitor page — alert management.
/// Maps to the React MonitorView component.
/// </summary>
public partial class MonitorViewModel : ObservableObject
{
    private readonly PowerHunterDatabase _database;
    private readonly IAlertService _alertService;
    private readonly AppUsageAlertEvaluator _alertEvaluator;
    private readonly PowerEstimationService _powerEstimation;
    private readonly IUsageStatsPermission _usagePermission;

    public MonitorViewModel(
        PowerHunterDatabase database,
        IAlertService alertService,
        AppUsageAlertEvaluator alertEvaluator,
        PowerEstimationService powerEstimation,
        IUsageStatsPermission usagePermission)
    {
        _database = database;
        _alertService = alertService;
        _alertEvaluator = alertEvaluator;
        _powerEstimation = powerEstimation;
        _usagePermission = usagePermission;
    }

    // ── Observable Properties ──

    [ObservableProperty]
    private bool _guardianEnabled;

    [ObservableProperty]
    private bool _tipsExpanded;

    [ObservableProperty]
    private ObservableCollection<BatteryAlert> _alerts = [];

    [ObservableProperty]
    private bool _hasAlerts;

    [ObservableProperty]
    private string _alertCountText = "Active Alerts (0)";

    [ObservableProperty]
    private ObservableCollection<BackgroundDrainEvent> _guardianFindings = [];

    [ObservableProperty]
    private bool _hasGuardianFindings;

    [ObservableProperty]
    private string _guardianStatusText = "Turn on Guardian to detect unusual background battery drain.";

    // ── Battery Tips (static data matching frontend) ──

    public List<string> BatteryTips { get; } =
    [
        "Reduce screen brightness or enable auto-brightness.",
        "Turn off location services for apps that don't need it.",
        "Disable background app refresh for non-essential apps.",
    ];

    // ── Commands ──

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        var settings = await _database.GetSettingsAsync();
        GuardianEnabled = settings.GuardianEnabled;

        await RefreshGuardianStateAsync();
        await RefreshAlertsAsync();
        await EvaluateCurrentUsageAsync();
    }

    [RelayCommand]
    private async Task ToggleGuardianAsync()
    {
        var settings = await _database.GetSettingsAsync();
        settings.GuardianEnabled = GuardianEnabled;
        await _database.SaveSettingsAsync(settings);
        await RefreshGuardianStateAsync();
        await EvaluateCurrentUsageAsync();
    }

    [RelayCommand]
    private void ToggleTips()
    {
        TipsExpanded = !TipsExpanded;
    }

    [RelayCommand]
    private async Task AddAlertAsync()
    {
        try
        {
            string? title = await PromptAsync(
                "New Alert",
                "Alert name:",
                placeholder: "High Usage Warning");

            if (string.IsNullOrWhiteSpace(title))
                return;

            string? thresholdStr = await PromptAsync(
                "Threshold",
                "App battery usage threshold (1-100):",
                placeholder: "15",
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(thresholdStr))
                return;

            if (!double.TryParse(thresholdStr, out double threshold))
            {
                await ShowInfoAsync("Invalid Threshold", "Please enter a number between 1 and 100.");
                return;
            }

            if (threshold < 1 || threshold > 100)
            {
                await ShowInfoAsync("Invalid Threshold", "Threshold must be between 1 and 100.");
                return;
            }

            await _alertService.CreateAlertAsync(
                title,
                $"Alert when any app exceeds {threshold}% battery usage share.",
                threshold);

            await RefreshAlertsAsync();
            await EvaluateCurrentUsageAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MonitorViewModel] AddAlert failed: {ex}");
            await ShowInfoAsync("Add Alert Failed", "Power Hunter couldn't open the alert form. Please try again.");
        }
    }

    [RelayCommand]
    private async Task ToggleAlertAsync(BatteryAlert? alert)
    {
        if (alert is null)
        {
            System.Diagnostics.Debug.WriteLine("[MonitorViewModel] ToggleAlert ignored because command parameter was null.");
            return;
        }

        await _alertService.ToggleAlertAsync(alert.Id, alert.IsEnabled);
        await RefreshAlertsAsync();
        await EvaluateCurrentUsageAsync();
    }

    [RelayCommand]
    private async Task DeleteAlertAsync(BatteryAlert? alert)
    {
        if (alert is null)
        {
            System.Diagnostics.Debug.WriteLine("[MonitorViewModel] DeleteAlert ignored because command parameter was null.");
            return;
        }

        bool confirmed = await ConfirmAsync(
            "Delete Alert",
            $"Remove \"{alert.Title}\"?",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        await _alertService.DeleteAlertAsync(alert.Id);
        await RefreshAlertsAsync();
    }

    private async Task RefreshAlertsAsync()
    {
        var alertList = await _alertService.GetAlertsAsync();
        Alerts = new ObservableCollection<BatteryAlert>(alertList);
        HasAlerts = alertList.Count > 0;
        AlertCountText = $"Active Alerts ({alertList.Count(a => a.IsEnabled)})";
    }

    private async Task RefreshGuardianStateAsync()
    {
        var findings = await _database.GetRecentBackgroundDrainEventsAsync();
        GuardianFindings = new ObservableCollection<BackgroundDrainEvent>(findings);
        HasGuardianFindings = findings.Count > 0;
        GuardianStatusText = BuildGuardianStatusText(findings.FirstOrDefault());
    }

    private async Task EvaluateCurrentUsageAsync()
    {
        var records = await _database.GetAppUsageAsync(DateTime.UtcNow.Date);
        await _alertEvaluator.EvaluateQuietlyAsync(records);
    }

    private string BuildGuardianStatusText(BackgroundDrainEvent? latestFinding)
    {
        if (!GuardianEnabled)
        {
            return "Turn on Guardian to detect unusual background battery drain.";
        }

        if (_usagePermission.IsSupported && !_usagePermission.IsGranted)
        {
            return "Grant Usage Access so Guardian can identify battery-hungry background apps.";
        }

        if (!_powerEstimation.IsCollectionAvailable)
        {
            return "Background drain detection is only available where system app usage stats are exposed.";
        }

        if (latestFinding is not null)
        {
            return $"Latest finding: {latestFinding.AppName} looked suspicious at {latestFinding.DetectedAt.ToLocalTime():MMM d HH:mm}.";
        }

        return "Guardian is monitoring for unusual background drain.";
    }

    private static async Task<string?> PromptAsync(
        string title,
        string message,
        string placeholder = "",
        Keyboard? keyboard = null)
    {
        var page = ResolveDialogPage();
        if (page is null)
            return null;

        return await MainThread.InvokeOnMainThreadAsync(() =>
            page.DisplayPromptAsync(
                title,
                message,
                placeholder: placeholder,
                keyboard: keyboard));
    }

    private static async Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept,
        string cancel)
    {
        var page = ResolveDialogPage();
        if (page is null)
            return false;

        return await MainThread.InvokeOnMainThreadAsync(() =>
            page.DisplayAlert(title, message, accept, cancel));
    }

    private static async Task ShowInfoAsync(string title, string message)
    {
        var page = ResolveDialogPage();
        if (page is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
            page.DisplayAlert(title, message, "OK"));
    }

    private static Page? ResolveDialogPage()
    {
        if (Shell.Current?.CurrentPage is Page currentPage)
            return currentPage;

        if (Shell.Current is Page shellPage)
            return shellPage;

        return Application.Current?.Windows
            .Select(window => window.Page)
            .FirstOrDefault(page => page is not null);
    }
}