namespace PowerHunter.Models;

/// <summary>
/// Top-level index for date-partitioned local storage.
/// </summary>
public sealed class DatePartitionIndex
{
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<DatePartitionEntry> Partitions { get; set; } = [];
}
