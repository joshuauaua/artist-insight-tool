/*
Shows the revenue distribution across albums.
SELECT Album.Title, SUM(Amount) FROM RevenueEntries JOIN Albums ON RevenueEntries.AlbumId = Albums.Id WHERE RevenueDate BETWEEN @StartDate AND @EndDate GROUP BY Album.Title
*/
namespace ArtistInsightTool.Apps.Views;

public class AlbumRevenueDistributionPieChartView(DateTime startDate, DateTime endDate) : ViewBase
{
    public override object Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var chart = UseState<object?>((object?)null!);
        var exception = UseState<Exception?>((Exception?)null!);

        this.UseEffect(async () =>
        {
            try
            {
                var db = factory.CreateDbContext();
                var data = await db.RevenueEntries
                    .Where(r => r.RevenueDate >= startDate && r.RevenueDate <= endDate)
                    .GroupBy(r => r.Album.Title)
                    .Select(g => new
                    {
                        AlbumTitle = g.Key,
                        Revenue = g.Sum(r => (double)r.Amount)
                    })
                    .ToListAsync();

                var totalRevenue = data.Sum(d => d.Revenue);

                PieChartTotal total = new(Format.Number(@"[<1000]0;[<10000]0.0,""K"";0,""K""", totalRevenue), "Revenue");

                chart.Set(data.ToPieChart(
                    dimension: d => d.AlbumTitle,
                    measure: d => d.Sum(x => x.Revenue),
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