using Ivy.Shared;
using ArtistInsightTool.Apps.Services;
using System.Linq;
using Ivy.Hooks;
using Ivy.Charts;

namespace ArtistInsightTool.Apps.Views;

public class TopSellingAssetsView : ViewBase
{
  private record PieData(string Dimension, double Measure);

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();

    // Using broad range (Last 10 years)
    var from = DateTime.Now.AddYears(-10);
    var to = DateTime.Now;

    var query = UseQuery("revenue_by_asset_top", async (ct) =>
    {
      var rawData = await service.GetRevenueByAssetAsync(from, to);
      // Map DTO to local PieData and ensure Value > 0 for chart safety
      return rawData.Select(d => new PieData(d.Label, d.Value)).Where(d => d.Measure > 0).ToList();
    });

    if (query.Loading) return Layout.Center().Height(Size.Units(75)).Add(Text.Label("Loading Chart..."));

    var data = query.Value ?? [];

    if (data.Count == 0) return Layout.Center().Height(Size.Units(75)).Add(Text.Label("No revenue data available."));

    var totalValue = data.Sum(d => d.Measure);

    return new Card(
        Layout.Vertical().Gap(5).Padding(10)
            .Add(Layout.Center().Add(Text.H4("Top Selling Assets")))
            .Add(new PieChart(data)
                .Pie(new Pie(nameof(PieData.Measure), nameof(PieData.Dimension))
                    .InnerRadius("40%")
                    .OuterRadius("80%")
                    .Animated(true)
                        .LabelList(new LabelList(nameof(PieData.Dimension))
                            .Position(Positions.Outside)
                            .Fill(Colors.Gray)
                            .FontSize(9))
                )
                .ColorScheme(ColorScheme.Default)
                .Tooltip(new Ivy.Charts.Tooltip().Animated(true))
                .Legend(new Legend().IconType(Legend.IconTypes.Rect))
                .Total(totalValue, "Total")
                .Height(Size.Units(50))
            )
    ).Height(Size.Units(85));
  }
}
