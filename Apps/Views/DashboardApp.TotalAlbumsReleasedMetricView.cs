/*
The total number of albums released during the selected date range.
COUNT(Album.Id WHERE Album.ReleaseDate BETWEEN StartDate AND EndDate)
*/
namespace ArtistInsightTool.Apps.Views;

using ArtistInsightTool.Apps.Services;

public class TotalAlbumsReleasedMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();

    async Task<MetricRecord> CalculateTotalAlbumsReleased()
    {
      var dto = await service.GetAlbumsReleasedAsync(fromDate, toDate);
      return dto != null ? new MetricRecord(
          MetricFormatted: dto.Value,
          TrendComparedToPreviousPeriod: dto.Trend,
          GoalAchieved: dto.GoalProgress,
          GoalFormatted: dto.GoalValue
      ) : new MetricRecord("0", null, null, null);
    }

    return new MetricView(
        "Total Albums Released",
        Icons.Disc,
        CalculateTotalAlbumsReleased
    );
  }
}