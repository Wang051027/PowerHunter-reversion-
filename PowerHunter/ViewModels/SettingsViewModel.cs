using CommunityToolkit.Mvvm.Messaging;

namespace PowerHunter.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// Maps to the React SettingsView component.
/// Settings are persisted to SQLite (unlike the React version which lost them on refresh).
/// </summary>
public partial class SettingsViewModel : ObservableObject, IRecipient<PermissionRefreshMessage>
{
    private readonly PowerHunterDatabase _database;
    private readonly DataLifecycleService _dataLifecycle;
    private readonly IUsageStatsPermission _usagePermission;

    public SettingsViewModel(
        PowerHunterDatabase database,
        DataLifecycleService dataLifecycle,
        IUsageStatsPermission usagePermission)
    {
        _database = database;
        _dataLifecycle = dataLifecycle;
        _usagePermission = usagePermission;

        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(PermissionRefreshMessage message)
    {
        RefreshPermissionState();
    }

    // ── Observable Properties ──

    [ObservableProperty]
    private bool _notifications;

    [ObservableProperty]
    private bool _darkModeEnabled;

    [ObservableProperty]
    private bool _nightAutoPowerSaving;

    [ObservableProperty]
    private bool _isUsageStatsSupported;

    [ObservableProperty]
    private bool _isUsageStatsGranted;

    public bool IsLightModeEnabled => !DarkModeEnabled;
    public string Version => "1.0.0";
    public string Build => "2024.02.11";
    public string NightAutoPowerSavingSchedule => "Automatically reduces background sampling from 22:00 to 07:00.";

    partial void OnDarkModeEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLightModeEnabled));
    }

    // ── Commands ──

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        var settings = await _database.GetSettingsAsync();
        DarkModeEnabled = GetEffectiveDarkModeEnabled(settings);
        Notifications = settings.NotificationsEnabled;
        NightAutoPowerSaving = settings.NightAutoPowerSavingEnabled;

        if (ThemePreferenceStore.HasExplicitTheme() || settings.ThemePreferenceConfigured)
        {
            ApplyTheme(DarkModeEnabled);
        }

        RefreshPermissionState();
    }

    /// <summary>
    /// Called from OnResume via WeakReferenceMessenger to re-check permission
    /// after the user returns from the system Settings screen.
    /// </summary>
    public void RefreshPermissionState()
    {
        IsUsageStatsSupported = _usagePermission.IsSupported;
        IsUsageStatsGranted = _usagePermission.IsGranted;
    }

    [RelayCommand]
    private async Task GrantUsageStatsAsync()
    {
        await _usagePermission.RequestAsync();
    }

    [RelayCommand]
    private Task SetLightThemeAsync()
    {
        return SetThemeAsync(isDarkMode: false);
    }

    [RelayCommand]
    private Task SetDarkThemeAsync()
    {
        return SetThemeAsync(isDarkMode: true);
    }

    [RelayCommand]
    private async Task ToggleNotificationsAsync()
    {
        var settings = await _database.GetSettingsAsync();
        settings.NotificationsEnabled = Notifications;
        await _database.SaveSettingsAsync(settings);
    }

    [RelayCommand]
    private async Task ToggleNightAutoPowerSavingAsync()
    {
        var settings = await _database.GetSettingsAsync();
        settings.NightAutoPowerSavingEnabled = NightAutoPowerSaving;
        await _database.SaveSettingsAsync(settings);
    }

    private async Task SetThemeAsync(bool isDarkMode)
    {
        DarkModeEnabled = isDarkMode;
        ApplyTheme(isDarkMode);
        ThemePreferenceStore.SaveTheme(isDarkMode);

        var settings = await _database.GetSettingsAsync();
        settings.DarkModeEnabled = isDarkMode;
        settings.ThemePreferenceConfigured = true;
        await _database.SaveSettingsAsync(settings);
    }

    private static bool GetEffectiveDarkModeEnabled(UserSettings settings)
    {
        if (ThemePreferenceStore.TryGetTheme(out var savedTheme))
            return savedTheme == AppTheme.Dark;

        if (settings.ThemePreferenceConfigured)
            return settings.DarkModeEnabled;

        return Application.Current?.RequestedTheme == AppTheme.Dark;
    }

    private static void ApplyTheme(bool isDarkMode)
    {
        if (Application.Current is null)
            return;

        Application.Current.UserAppTheme = isDarkMode ? AppTheme.Dark : AppTheme.Light;
    }

    [RelayCommand]
    private async Task ArchiveDataAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Archive Historical Data",
            "Move detailed records older than 30 days into local date-based archives and keep recent data online?",
            "Archive", "Cancel");

        if (!confirmed) return;

        var result = await _dataLifecycle.ArchiveHistoricalDataAsync(30);

        if (!result.HadWork)
        {
            await Shell.Current.DisplayAlert("Up to Date", "No records older than 30 days were found.", "OK");
            return;
        }

        var releasedMb = result.ReleasedDatabaseBytes / 1024d / 1024d;
        var message =
            $"Archived {result.ArchivedDayCount} day(s).\n" +
            $"Battery snapshots: {result.BatteryRecordCount}\n" +
            $"App usage rows: {result.AppUsageRecordCount}\n" +
            $"Database space reclaimed: {releasedMb:F2} MB";

        await Shell.Current.DisplayAlert("Archive Complete", message, "OK");
    }
}
