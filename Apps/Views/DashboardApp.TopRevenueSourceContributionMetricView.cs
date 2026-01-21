/*
The percentage of total revenue contributed by the top revenue source during the selected date range.
(MAX(SUM(RevenueEntry.Amount WHERE RevenueEntry.SourceId = RevenueSource.Id AND RevenueEntry.RevenueDate BETWEEN StartDate AND EndDate)) / SUM(RevenueEntry.Amount WHERE RevenueEntry.RevenueDate BETWEEN StartDate AND EndDate)) * 100
*/
namespace ArtistInsightTool.Apps.Views;

using ArtistInsightTool.Apps.Services;

public class TopRevenueSourceContributionMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
  public override object? Build()
  {
    var service = UseService<ArtistInsightService>();

    async Task<MetricRecord> CalculateTopRevenueSourceContribution()
    {
      var dto = await service.GetTopSourceContributionAsync(fromDate, toDate);
      return dto != null ? new MetricRecord(
          MetricFormatted: dto.Value,
          TrendComparedToPreviousPeriod: dto.Trend,
          GoalAchieved: dto.GoalProgress,
          GoalFormatted: dto.GoalValue
      ) : new MetricRecord("0.00%", null, null, null);
    }

    return new MetricView(
        "Top Revenue Source Contribution",
        null,
        _ => UseQuery("dashboard_top_source", _ => CalculateTopRevenueSourceContribution())
    );
  }
}