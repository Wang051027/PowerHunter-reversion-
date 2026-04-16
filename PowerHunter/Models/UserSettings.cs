using SQLite;

namespace PowerHunter.Models;

/// <summary>
/// Persisted user preferences (dark mode, notifications, guardian mode).
/// Uses a single-row pattern — only one row with Id=1 exists.
/// </summary>
[Table("UserSettings")]
public sealed class UserSettings
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    public bool DarkModeEnabled { get; set; } = true;
    public bool ThemePreferenceConfigured { get; set; }
    public bool NotificationsEnabled { get; set; }
    public bool GuardianEnabled { get; set; }
    public bool NightAutoPowerSavingEnabled { get; set; }
}
