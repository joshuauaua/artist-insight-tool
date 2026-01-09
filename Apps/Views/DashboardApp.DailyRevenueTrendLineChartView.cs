/*
Tracks the total revenue generated daily within the selected date range.
SELECT RevenueDate, SUM(Amount) FROM RevenueEntries WHERE RevenueDate BETWEEN @StartDate AND @EndDate GROUP BY RevenueDate ORDER BY RevenueDate
*/
namespace ArtistInsightTool.Apps.Views;

public class DailyRevenueTrendLineChartView(DateTime startDate, DateTime endDate) : ViewBase
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
                    .Where(e => e.RevenueDate >= startDate && e.RevenueDate <= endDate)
                    .GroupBy(e => e.RevenueDate.Date)
                    .Select(g => new
                    {
                        Date = g.Key.ToString("d MMM"),
                        TotalRevenue = g.Sum(e => (double)e.Amount)
                    })
                    .ToListAsync();

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

        var card = new Card().Title("Daily Revenue Trend").Height(Size.Units(80));

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