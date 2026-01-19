/*
The average revenue generated per track during the selected date range.
SUM(RevenueEntry.Amount WHERE RevenueEntry.RevenueDate BETWEEN StartDate AND EndDate) / COUNT(DISTINCT RevenueEntry.TrackId)
*/
namespace ArtistInsightTool.Apps.Views;

using ArtistInsightTool.Apps.Services;

public class RevenuePerTrackMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();

    async Task<MetricRecord> CalculateRevenuePerTrack()
    {
      var dto = await service.GetRevenuePerTrackAsync(fromDate, toDate);
      return dto != null ? new MetricRecord(
          MetricFormatted: dto.Value,
          TrendComparedToPreviousPeriod: dto.Trend,
          GoalAchieved: dto.GoalProgress,
          GoalFormatted: dto.GoalValue
      ) : new MetricRecord("0", null, null, null);
    }

    return new MetricView(
        "Revenue Per Track",
        null,
        CalculateRevenuePerTrack
    );
  }
}