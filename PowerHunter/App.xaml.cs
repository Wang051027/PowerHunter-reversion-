namespace PowerHunter;

public partial class App : Application
{
    private readonly PowerHunterDatabase _database;

    public App(PowerHunterDatabase database)
    {
        _database = database;

        InitializeComponent();
        ApplySavedTheme();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        window.Created += async (_, _) =>
        {
            // Start in-app battery monitoring while the app window is active.
            try
            {
                await RestoreThemePreferenceFromDatabaseAsync();

                var services = Handler?.MauiContext?.Services;
                var batteryService = services?.GetService<IBatteryService>();
                if (batteryService is not null)
                {
                    await batteryService.StartMonitoringAsync(BatteryRefreshDefaults.InAppSnapshotInterval);
                }

                var dataLifecycle = services?.GetService<DataLifecycleService>();
                if (dataLifecycle is not null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await dataLifecycle.ArchiveHistoricalDataAsync(30);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[App] Archive maintenance failed: {ex}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] StartMonitoring failed: {ex}");
            }
        };

        window.Destroying += (_, _) =>
        {
            try
            {
                var batteryService = Handler?.MauiContext?.Services.GetService<IBatteryService>();
                batteryService?.StopMonitoring();
            }
            catch
            {
                // Best-effort cleanup
            }
        };

        return window;
    }

    private void ApplySavedTheme()
    {
        try
        {
            UserAppTheme = ThemePreferenceStore.TryGetTheme(out var theme)
                ? theme
                : AppTheme.Unspecified;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Failed to restore theme preference: {ex}");
            UserAppTheme = AppTheme.Unspecified;
        }
    }

    private async Task RestoreThemePreferenceFromDatabaseAsync()
    {
        if (ThemePreferenceStore.HasExplicitTheme())
            return;

        try
        {
            var settings = await _database.GetSettingsAsync();
            if (!settings.ThemePreferenceConfigured)
                return;

            ThemePreferenceStore.SaveTheme(settings.DarkModeEnabled);
            UserAppTheme = settings.DarkModeEnabled ? AppTheme.Dark : AppTheme.Light;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Failed to migrate theme preference from database: {ex}");
        }
    }
}
