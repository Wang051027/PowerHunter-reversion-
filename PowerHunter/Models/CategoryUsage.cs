namespace PowerHunter.Models;

/// <summary>
/// Aggregated battery usage by category for chart display.
/// This is a view model / DTO — not persisted directly in SQLite.
/// </summary>
public sealed record CategoryUsage(
    string Name,
    double Percentage,
    string HexColor
);
