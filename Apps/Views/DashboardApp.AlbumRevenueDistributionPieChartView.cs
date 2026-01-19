/*
Shows the revenue distribution across albums.
SELECT Album.Title, SUM(Amount) FROM RevenueEntries JOIN Albums ON RevenueEntries.AlbumId = Albums.Id WHERE RevenueDate BETWEEN @StartDate AND @EndDate GROUP BY Album.Title
*/
namespace ArtistInsightTool.Apps.Views;

using ArtistInsightTool.Apps.Services;

public class AlbumRevenueDistributionPieChartView(DateTime startDate, DateTime endDate) : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var chart = UseState<object?>((object?)null!);
    var exception = UseState<Exception?>((Exception?)null!);

    this.UseEffect(async () =>
    {
      try
      {
        var data = await service.GetRevenueByAlbumAsync(startDate, endDate);

        var totalRevenue = data.Sum(d => d.Value);

        PieChartTotal total = new(Format.Number(@"[<1000]0;[<10000]0.0,""K"";0,""K""", totalRevenue), "Revenue");

        chart.Set(data.ToPieChart(
                dimension: d => d.Label,
                measure: d => d.Sum(x => x.Value),
                PieChartStyles.Dashboard,
                total));
      }
      catch (Exception ex)
      {
        exception.Set(ex);
      }
    }, []);

    var card = new Card().Title("Album Revenue Distribution").Height(Size.Units(80));

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