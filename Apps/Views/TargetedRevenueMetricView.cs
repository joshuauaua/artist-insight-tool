using Ivy.Shared;
using ArtistInsightTool.Apps.Services;
using System.Globalization;
using System;
using System.Threading.Tasks;
using Ivy.Hooks;

namespace ArtistInsightTool.Apps.Views;

public class TargetedRevenueMetricView(decimal targetRevenue) : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();

    async Task<MetricRecord> CalculateTargetedRevenue()
    {
      // Fetch total revenue for a wide range (simulating lifetime)
      var dto = await service.GetTotalRevenueAsync(DateTime.Now.AddYears(-10), DateTime.Now);

      decimal currentRevenueValue = 0;
      if (dto != null && decimal.TryParse(dto.Value.Replace("$", "").Replace("USD", "").Replace("SEK", "").Trim(), out var val))
      {
        currentRevenueValue = val;
      }

      var progress = targetRevenue > 0 ? (double)(currentRevenueValue / targetRevenue) : 0;

      return new MetricRecord(
          MetricFormatted: currentRevenueValue.ToString("C0", CultureInfo.GetCultureInfo("sv-SE")),
          TrendComparedToPreviousPeriod: dto?.Trend,
          GoalAchieved: progress,
          GoalFormatted: targetRevenue.ToString("C0", CultureInfo.GetCultureInfo("sv-SE"))
      );
    }

    return new MetricView(
        "Targeted Revenue",
        Icons.Target,
        _ => UseQuery("dashboard_targeted_revenue", _ => CalculateTargetedRevenue())
    );
  }
}
