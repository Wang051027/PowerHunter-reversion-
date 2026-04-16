namespace PowerHunter.Services;

public sealed class NullAppIconService : IAppIconService
{
    public ImageSource? GetIcon(string appId) => null;
    public ImageSource? GetDefaultIcon() => null;
}
