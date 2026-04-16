using Microsoft.Maui.Dispatching;

namespace PowerHunter.Views;

public partial class StatsPage : ContentPage
{
    private readonly StatsViewModel _viewModel;
    private readonly IDispatcherTimer _refreshTimer;
    private bool _isRefreshing;

    public StatsPage(StatsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _refreshTimer = Dispatcher.CreateTimer();
        _refreshTimer.Interval = BatteryRefreshDefaults.UiRefreshInterval;
        _refreshTimer.Tick += async (_, _) => await RefreshDataAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _refreshTimer.Start();
        await RefreshDataAsync();
    }

    protected override void OnDisappearing()
    {
        _refreshTimer.Stop();
        base.OnDisappearing();
    }

    private async Task RefreshDataAsync()
    {
        if (_isRefreshing) return;

        _isRefreshing = true;
        try
        {
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StatsPage] LoadData failed: {ex}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async void OnAppsClicked(object? sender, EventArgs e)
        => await NavigateAsync("//apps");

    private async void OnStatsClicked(object? sender, EventArgs e)
    {
        await RefreshDataAsync();
        await NavigateAsync("//stats");
    }

    private async void OnMonitorClicked(object? sender, EventArgs e)
        => await NavigateAsync("//monitor");

    private async void OnSettingsClicked(object? sender, EventArgs e)
        => await NavigateAsync("//settings");

    private async void OnStatsTapped(object? sender, TappedEventArgs e)
    {
        await RefreshDataAsync();
        await NavigateAsync("//stats");
    }

    private async void OnAppsTapped(object? sender, TappedEventArgs e)
        => await NavigateAsync("//apps");

    private async void OnMonitorTapped(object? sender, TappedEventArgs e)
        => await NavigateAsync("//monitor");

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
        => await NavigateAsync("//settings");

    private static async Task NavigateAsync(string route)
    {
        if (Shell.Current is null)
            return;

        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StatsPage] Navigation to {route} failed: {ex}");
        }
    }
}
