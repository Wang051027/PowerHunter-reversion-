namespace PowerHunter.Services;

public static class ThemePreferenceStore
{
    private const string ThemeConfiguredKey = "settings.theme.configured";
    private const string ThemeIsDarkKey = "settings.theme.is-dark";

    public static bool HasExplicitTheme()
    {
        return Preferences.Default.Get(ThemeConfiguredKey, false);
    }

    public static bool TryGetTheme(out AppTheme theme)
    {
        if (!HasExplicitTheme())
        {
            theme = AppTheme.Unspecified;
            return false;
        }

        theme = Preferences.Default.Get(ThemeIsDarkKey, true) ? AppTheme.Dark : AppTheme.Light;
        return true;
    }

    public static void SaveTheme(bool isDarkMode)
    {
        Preferences.Default.Set(ThemeConfiguredKey, true);
        Preferences.Default.Set(ThemeIsDarkKey, isDarkMode);
    }
}
