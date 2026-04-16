namespace PowerHunter.Services;

public interface IAppVisibilityService
{
    bool IsInForeground { get; }
}
