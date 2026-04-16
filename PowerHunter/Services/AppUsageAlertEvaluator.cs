using PowerHunter.Models;

namespace PowerHunter.Services;

/// <summary>
/// Runs alert evaluation against the latest app usage data without letting
/// notification failures break foreground refresh flows.
/// </summary>
public sealed class AppUsageAlertEvaluator
{
    private readonly IAlertService _alertService;

    public AppUsageAlertEvaluator(IAlertService alertService)
    {
        _alertService = alertService;
    }

    public async Task EvaluateQuietlyAsync(IEnumerable<AppUsageRecord> usageRecords)
    {
        var records = usageRecords as IReadOnlyCollection<AppUsageRecord> ?? usageRecords.ToList();
        if (records.Count == 0)
            return;

        try
        {
            await _alertService.EvaluateAlertsAsync(records);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppUsageAlertEvaluator] Alert evaluation error: {ex}");
        }
    }
}
