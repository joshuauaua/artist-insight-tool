/*
Displays the proportion of revenue generated from each source.
SELECT Source.DescriptionText, SUM(Amount) FROM RevenueEntries JOIN RevenueSources ON RevenueEntries.SourceId = RevenueSources.Id WHERE RevenueDate BETWEEN @StartDate AND @EndDate GROUP BY Source.DescriptionText
*/
namespace ArtistInsightTool.Apps.Views;

public class RevenueBySourcePieChartView(DateTime startDate, DateTime endDate) : ViewBase
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
                    .Where(re => re.RevenueDate >= startDate && re.RevenueDate <= endDate)
                    .GroupBy(re => re.Source.DescriptionText)
                    .Select(g => new
                    {
                        Source = g.Key,
                        Revenue = g.Sum(re => (double)re.Amount)
                    })
                    .ToListAsync();

                var totalRevenue = data.Sum(d => d.Revenue);

                PieChartTotal total = new(Format.Number(@"[<1000]0;[<10000]0.0,""K"";0,""K""", totalRevenue), "Revenue");

                chart.Set(data.ToPieChart(
                    dimension: d => d.Source,
                    measure: d => d.Sum(x => x.Revenue),
                    PieChartStyles.Dashboard,
                    total));
            }
            catch (Exception ex)
            {
                exception.Set(ex);
            }
        }, []);

        var card = new Card().Title("Revenue by Source").Height(Size.Units(80));

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