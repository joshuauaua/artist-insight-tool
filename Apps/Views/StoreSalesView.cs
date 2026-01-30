using Ivy.Shared;
using ArtistInsightTool.Apps.Services;
using System.Linq;
using Ivy.Hooks;
using Ivy.Charts;

namespace ArtistInsightTool.Apps.Views;

public class StoreSalesView : ViewBase
{
  private record ChartData(string Label, double Value);

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();

    // Broad date range (Last 10 years)
    var from = DateTime.Now.AddYears(-10);
    var to = DateTime.Now;

    var query = UseQuery("revenue_by_source_sorted", async (ct) =>
    {
      var rawData = await service.GetRevenueBySourceAsync(from, to);
      return rawData.Select(d => new ChartData(d.Label, d.Value)).Where(d => d.Value > 0).ToList();
    });

    if (query.Loading) return Layout.Center().Height(Size.Units(75)).Add(Text.Label("Loading Chart..."));

    var data = query.Value ?? [];

    if (data.Count == 0) return Layout.Center().Height(Size.Units(75)).Add(Text.Label("No revenue data available."));

    return new Card(
        Layout.Vertical().Gap(5).Padding(10)
            .Add(Layout.Center().Add(Text.H4("Total Sales by Store (SEK)")))
            .Add(Layout.Vertical().Height(Size.Units(50))
                .Add(data.ToBarChart()
                    .Dimension("Label", e => e.Label)
                    .Measure("Value", e => e.Sum(f => f.Value))
                    .SortBy(SortOrder.Ascending)
                )
            )
    ).Height(Size.Units(85));
  }
}
