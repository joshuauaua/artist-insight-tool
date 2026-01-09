/*
Compares the daily revenue trends of different artists.
SELECT Artist.Name, RevenueDate, SUM(Amount) FROM RevenueEntries JOIN Artists ON RevenueEntries.ArtistId = Artists.Id WHERE RevenueDate BETWEEN @StartDate AND @EndDate GROUP BY Artist.Name, RevenueDate
*/
namespace ArtistInsightTool.Apps.Views;

public class ArtistRevenueComparisonLineChartView(DateTime startDate, DateTime endDate) : ViewBase
{
  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var chart = UseState<object?>((object?)null);
    var exception = UseState<Exception?>((Exception?)null);

    this.UseEffect(async () =>
    {
      try
      {
        var db = factory.CreateDbContext();
        var data = await db.RevenueEntries
                .Where(r => r.RevenueDate >= startDate && r.RevenueDate <= endDate)
                .GroupBy(r => new { r.Artist.Name, r.RevenueDate })
                .Select(g => new
                {
                  Artist = g.Key.Name,
                  Date = g.Key.RevenueDate.Date.ToString("d MMM"),
                  Revenue = g.Sum(r => (double)r.Amount)
                })
                .ToListAsync();

        chart.Set(data.ToLineChart(
                e => e.Date,
                [e => e.Sum(f => f.Revenue)],
                LineChartStyles.Dashboard
            ));
      }
      catch (Exception ex)
      {
        exception.Set(ex);
      }
    }, []);

    var card = new Card().Title("Artist Revenue Comparison").Height(Size.Units(80));

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