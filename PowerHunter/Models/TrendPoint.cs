namespace PowerHunter.Models;

/// <summary>
/// A single data point for the battery trend chart.
/// Date is kept for proper chronological ordering.
/// </summary>
public sealed record TrendPoint(
    string Label,
    double Value,
    DateTime Date
);
