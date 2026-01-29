using Ivy.Shared;
using ArtistInsightTool.Apps.Services;
using System.Globalization;
using System.Linq;
using Ivy.Hooks;
using Ivy.Charts;

namespace ArtistInsightTool.Apps.Views;

public class AssetPieChartView(string categoryFilter) : ViewBase
{
  private record PieData(string Dimension, double Measure);

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();

    var query = UseQuery($"asset_pie_{categoryFilter}", async (ct) =>
    {
      var assets = await service.GetAssetsAsync();

      var filtered = string.Equals(categoryFilter, "All", StringComparison.OrdinalIgnoreCase)
          ? assets
          : assets.Where(a => string.Equals(a.Category, categoryFilter, StringComparison.OrdinalIgnoreCase)).ToList();

      // Group by name (in case of duplicate names, though usually names are IDs here) and sum net amount
      return filtered
          .GroupBy(a => a.Name ?? "Unnamed Asset")
          .Select(g => new PieData(g.Key, (double)g.Sum(a => a.NetAmount)))
          .Where(d => d.Measure > 0)
          .OrderByDescending(d => d.Measure)
          .ToList();
    });

    if (query.Loading) return Layout.Center().Height(Size.Units(75)).Add(Text.Label("Loading Chart..."));

    var data = query.Value ?? [];
    var totalValue = data.Sum(d => d.Measure);

    return new Card(
        Layout.Vertical().Gap(10).Padding(10)
            .Add(Text.H4($"Asset Breakdown ({categoryFilter})"))
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
                .Total(totalValue, "Total Revenue")
                .Height(Size.Units(55))
            )
    ).Height(Size.Units(75));
  }
}
