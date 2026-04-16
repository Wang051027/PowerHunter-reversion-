namespace PowerHunter.Views;

public partial class MonitorPage : ContentPage
{
    private readonly MonitorViewModel _viewModel;

    public MonitorPage(MonitorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MonitorPage] LoadData failed: {ex}");
        }
    }
}
