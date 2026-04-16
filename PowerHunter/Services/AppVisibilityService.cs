namespace PowerHunter.Services;

public sealed class AppVisibilityService : IAppVisibilityService
{
    private volatile bool _isInForeground;

    public bool IsInForeground => _isInForeground;

    public void MarkForeground()
    {
        _isInForeground = true;
    }

    public void MarkBackground()
    {
        _isInForeground = false;
    }
}
