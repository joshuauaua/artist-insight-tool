/*
Shows the daily revenue trend for the top 5 tracks by total revenue.
SELECT Track.Title, RevenueDate, SUM(Amount) FROM RevenueEntries JOIN Tracks ON RevenueEntries.TrackId = Tracks.Id WHERE RevenueDate BETWEEN @StartDate AND @EndDate GROUP BY Track.Title, RevenueDate ORDER BY SUM(Amount) DESC LIMIT 5
*/
namespace ArtistInsightTool.Apps.Views;

using ArtistInsightTool.Apps.Services;

public class TopPerformingTracksLineChartView(DateTime startDate, DateTime endDate) : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var chart = UseState<object?>((object?)null);
    var exception = UseState<Exception?>((Exception?)null);

    this.UseEffect(async () =>
    {
      try
      {
        var rawData = await service.GetTopPerformingTracksPointsAsync(startDate, endDate);
        var data = rawData.Select(d => new
        {
          TrackTitle = d.TrackTitle,
          Date = d.Date.ToString("d MMM"),
          TotalRevenue = d.Revenue
        }).ToList();

        chart.Set(data.ToLineChart(
                e => e.Date,
                [e => e.Sum(f => f.TotalRevenue)],
                LineChartStyles.Dashboard));
      }
      catch (Exception ex)
      {
        exception.Set(ex);
      }
    }, []);

    var card = new Card().Title("Top Performing Tracks").Height(Size.Units(80));

    if (exception.Value != null)
    {
      return card | new ErrorTeaserView(exception.Value);
    }

    if (chart.Value == null)
    {
      return card | new Skeleton();
    }

    return card | chart.Value;
  }
}