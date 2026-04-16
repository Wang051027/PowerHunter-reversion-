namespace PowerHunter.Models;

/// <summary>
/// View-facing app metadata used by the stats dashboard consumer list.
/// </summary>
public sealed partial class TrackedAppInfo : ObservableObject
{
    public TrackedAppInfo(
        string id,
        string name,
        string fallbackGlyph,
        Color badgeColor,
        Color iconColor,
        ImageSource? iconSource = null,
        double usagePercentage = 0,
        double changePercentage = 0,
        string categoryLabel = "",
        bool isOfficialPowerData = false)
    {
        Id = id;
        Name = name;
        FallbackGlyph = fallbackGlyph;
        BadgeColor = badgeColor;
        IconColor = iconColor;
        IconSource = iconSource;
        UsagePercentage = usagePercentage;
        ChangePercentage = changePercentage;
        CategoryLabel = categoryLabel;
        IsOfficialPowerData = isOfficialPowerData;
    }

    public string Id { get; }

    public string Name { get; }

    public string FallbackGlyph { get; }

    public Color BadgeColor { get; }

    public Color IconColor { get; }

    public ImageSource? IconSource { get; }

    public double UsagePercentage { get; }

    public double ChangePercentage { get; }

    public string CategoryLabel { get; }

    public bool IsOfficialPowerData { get; }

    private bool _isSelected;

    public bool HasIconSource => IconSource is not null;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsTrendUp => ChangePercentage > 0.05;

    public bool IsTrendDown => ChangePercentage < -0.05;

    public bool IsTrendFlat => !IsTrendUp && !IsTrendDown;

    public string UsagePercentageText => $"{UsagePercentage:0.#}%";

    public string ChangePercentageText => $"{Math.Abs(ChangePercentage):0.#}%";

    public string TrendGlyph => IsTrendUp ? "↑" : IsTrendDown ? "↓" : "•";

    public string SecondaryLabel => string.IsNullOrWhiteSpace(CategoryLabel)
        ? "Active Process"
        : CategoryLabel;

    public Color TrendColor => IsTrendUp
        ? Color.FromArgb("#BA1A1A")
        : IsTrendDown
            ? Color.FromArgb("#006B54")
            : Color.FromArgb("#546067");
}
