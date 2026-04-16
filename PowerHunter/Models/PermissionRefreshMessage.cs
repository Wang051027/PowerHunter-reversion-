using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PowerHunter.Models;

/// <summary>
/// Sent by MainActivity.OnResume so ViewModels can re-check
/// usage stats permission after the user returns from Settings.
/// </summary>
public sealed class PermissionRefreshMessage : ValueChangedMessage<bool>
{
    public PermissionRefreshMessage() : base(true) { }
}
