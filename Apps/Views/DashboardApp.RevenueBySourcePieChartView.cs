/*
Displays the proportion of revenue generated from each source.
SELECT Source.DescriptionText, SUM(Amount) FROM RevenueEntries JOIN RevenueSources ON RevenueEntries.SourceId = RevenueSources.Id WHERE RevenueDate BETWEEN @StartDate AND @EndDate GROUP BY Source.DescriptionText
*/
namespace ArtistInsightTool.Apps.Views;

using ArtistInsightTool.Apps.Services;

public class RevenueBySourcePieChartView(DateTime startDate, DateTime endDate) : ViewBase
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
        var data = await service.GetRevenueBySourceAsync(startDate, endDate);

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