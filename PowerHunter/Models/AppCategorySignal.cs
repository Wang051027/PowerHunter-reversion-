namespace PowerHunter.Models;

/// <summary>
/// Category evidence collected from the operating system for a package.
/// Primary hints come from installer/manifest category metadata, while
/// behavior tags are supplementary signals from launcher registration,
/// declared capabilities, and observed usage patterns.
/// </summary>
public sealed record AppCategorySignal(
    string? PrimaryCategoryHint = null,
    IReadOnlyList<string>? BehaviorTags = null
);
