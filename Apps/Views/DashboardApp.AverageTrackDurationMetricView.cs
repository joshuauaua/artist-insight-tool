/*
The average duration of all tracks created by artists.
AVG(Track.Duration)
*/
namespace ArtistInsightTool.Apps.Views;

using ArtistInsightTool.Apps.Services;

public class AverageTrackDurationMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();

    async Task<MetricRecord> CalculateAverageTrackDuration()
    {
      var dto = await service.GetAvgTrackDurationAsync(fromDate, toDate);
      return dto != null ? new MetricRecord(
          MetricFormatted: dto.Value,
          TrendComparedToPreviousPeriod: dto.Trend,
          GoalAchieved: dto.GoalProgress,
          GoalFormatted: dto.GoalValue
      ) : new MetricRecord("0.00 seconds", null, null, null);
    }

    return new MetricView(
        "Average Track Duration",
        Icons.Clock,
        CalculateAverageTrackDuration
    );
  }
}