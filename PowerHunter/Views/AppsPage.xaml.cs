using Microsoft.Maui.Dispatching;

namespace PowerHunter.Views;

public partial class AppsPage : ContentPage
{
    private readonly AppsViewModel _viewModel;
    private readonly IDispatcherTimer _refreshTimer;
    private bool _isRefreshing;

    public AppsPage(AppsViewModel viewModel)
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
            System.Diagnostics.Debug.WriteLine($"[AppsPage] LoadData failed: {ex}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }
}
