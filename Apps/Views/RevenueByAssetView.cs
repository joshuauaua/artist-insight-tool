using Ivy;
using Ivy.Shared;
using Ivy.Charts;
using ArtistInsightTool.Apps.Services;
using ArtistInsightTool.Connections.ArtistInsightTool;
using System.Text.Json;
using System.Globalization;

namespace ArtistInsightTool.Apps.Views;

public class RevenueByAssetView : ViewBase
{
  private record ChartRow(string Month, DateTime SortDate, string AssetName, double Amount);

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var dataQuery = UseQuery("revenue_asset_history", async () =>
    {
      return await service.GetRevenueByAssetHistoryAsync(DateTime.Now.AddYears(-2), DateTime.Now);
    });

    if (dataQuery.Loading) return Layout.Center().Add(Text.Label("Loading asset history...").Muted());

    var rawData = dataQuery.Value ?? [];
    if (rawData.Count == 0) return Layout.Center().Add(Text.Label("No historical data found."));

    // Get all keys except "Month" and "_SortDate"
    var assetNames = rawData.SelectMany(d => d.Keys).Distinct()
                         .Where(k => k != "Month" && k != "_SortDate")
                         .OrderBy(x => x)
                         .ToList();

    // Flatten data for chart consumption
    var flatData = new List<ChartRow>();
    foreach (var row in rawData)
    {
      if (row.TryGetValue("Month", out var mObj) && mObj is string month)
      {
        var sortDate = row.ContainsKey("_SortDate") && row["_SortDate"] is DateTime dt ? dt : DateTime.MinValue;
        foreach (var name in assetNames)
        {
          double val = 0;
          if (row.TryGetValue(name, out var vVar))
          {
            if (vVar is JsonElement je && je.ValueKind == JsonValueKind.Number) val = je.GetDouble();
            else if (vVar is double d) val = d;
            else if (vVar is int i) val = (double)i;
          }
          if (val > 0) flatData.Add(new ChartRow(month, sortDate, name, val));
        }
      }
    }

    // Build chart
    var chartBuilder = flatData.AsQueryable().ToLineChart(style: LineChartStyles.Dashboard)
                           .Dimension("Month", e => e.Month);

    foreach (var name in assetNames)
    {
      chartBuilder.Measure(name, g => g.Where(r => r.AssetName == name).Sum(r => r.Amount));
    }

    chartBuilder.Toolbox();

    return new Card(
        Layout.Vertical().Gap(5).Padding(10)
            .Add(Layout.Center().Add(Text.H4("Asset Revenue History (SEK)")))
            .Add(Layout.Vertical().Height(Size.Units(500))
                .Add(chartBuilder)
            )
    ).Height(Size.Units(600));
  }
}
