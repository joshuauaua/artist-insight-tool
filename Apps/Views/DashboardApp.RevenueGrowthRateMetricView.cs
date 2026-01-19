/*
The percentage change in revenue compared to the previous date range.
((SUM(RevenueEntry.Amount WHERE RevenueEntry.RevenueDate BETWEEN StartDate AND EndDate) - SUM(RevenueEntry.Amount WHERE RevenueEntry.RevenueDate BETWEEN PreviousStartDate AND PreviousEndDate)) / SUM(RevenueEntry.Amount WHERE RevenueEntry.RevenueDate BETWEEN PreviousStartDate AND PreviousEndDate)) * 100
*/
namespace ArtistInsightTool.Apps.Views;

using ArtistInsightTool.Apps.Services;

public class RevenueGrowthRateMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
  public override object? Build()
  {
    var service = UseService<ArtistInsightService>();

    async Task<MetricRecord> CalculateRevenueGrowthRate()
    {
      var dto = await service.GetGrowthRateAsync(fromDate, toDate);
      return dto != null ? new MetricRecord(
          MetricFormatted: dto.Value,
          TrendComparedToPreviousPeriod: dto.Trend,
          GoalAchieved: dto.GoalProgress,
          GoalFormatted: dto.GoalValue
      ) : new MetricRecord("0.00", null, null, null);
    }

    return new MetricView(
        "Revenue Growth Rate",
        Icons.TrendingUp,
        CalculateRevenueGrowthRate
    );
  }
}