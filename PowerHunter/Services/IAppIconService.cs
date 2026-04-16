namespace PowerHunter.Services;

/// <summary>
/// Resolves an app package identifier to the platform's native app icon when available.
/// </summary>
public interface IAppIconService
{
    ImageSource? GetIcon(string appId);
    ImageSource? GetDefaultIcon();
}
