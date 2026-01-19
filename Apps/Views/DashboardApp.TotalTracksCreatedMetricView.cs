/*
The total number of tracks created by artists during the selected date range.
COUNT(Track.Id WHERE Track.CreatedAt BETWEEN StartDate AND EndDate)
*/
namespace ArtistInsightTool.Apps.Views;

using ArtistInsightTool.Apps.Services;

public class TotalTracksCreatedMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();

    async Task<MetricRecord> CalculateTotalTracksCreated()
    {
      var dto = await service.GetTracksCreatedAsync(fromDate, toDate);
      return dto != null ? new MetricRecord(
          MetricFormatted: dto.Value,
          TrendComparedToPreviousPeriod: dto.Trend,
          GoalAchieved: dto.GoalProgress,
          GoalFormatted: dto.GoalValue
      ) : new MetricRecord("0", null, null, null);
    }

    return new MetricView(
        "Total Tracks Created",
        Icons.Music,
        CalculateTotalTracksCreated
    );
  }
}