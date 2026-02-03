using Ivy.Shared;
using ArtistInsightTool.Apps.Services;
using System.Linq;
using Ivy.Hooks;
using Ivy.Charts;
using System.Text.Json;
using System.Collections.Generic;

namespace ArtistInsightTool.Apps.Views;

public class NetVsGrossByCategoryView : ViewBase
{
  private record ChartData(string Category, double Gross, double Net);

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();

    var query = UseQuery("net_vs_gross_by_category", async (ct) =>
    {
      var entries = await service.GetRevenueEntriesAsync();

      var categoryStats = new Dictionary<string, (double Gross, double Net)>();

      foreach (var entry in entries)
      {
        if (string.IsNullOrEmpty(entry.JsonData)) continue;

        double entryGross = 0;
        double entryNet = (double)entry.Amount;

        try
        {
          using var doc = JsonDocument.Parse(entry.JsonData);
          if (doc.RootElement.ValueKind == JsonValueKind.Array)
          {
            foreach (var sheet in doc.RootElement.EnumerateArray())
            {
              if (sheet.TryGetProperty("Rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
              {
                foreach (var row in rows.EnumerateArray())
                {
                  // Try to find Gross
                  if (row.TryGetProperty("Gross", out var gProp))
                  {
                    if (double.TryParse(gProp.GetString(), out var val)) entryGross += val;
                  }
                  else if (row.TryGetProperty("Sale Price", out gProp))
                  {
                    if (double.TryParse(gProp.GetString(), out var val)) entryGross += val;
                  }
                  else if (row.TryGetProperty("Price", out gProp))
                  {
                    if (double.TryParse(gProp.GetString(), out var val)) entryGross += val;
                  }
                }
              }
            }
          }
        }
        catch { }

        var cat = entry.Category ?? "Other";
        if (!categoryStats.ContainsKey(cat)) categoryStats[cat] = (0, 0);

        var current = categoryStats[cat];
        categoryStats[cat] = (current.Gross + entryGross, current.Net + entryNet);
      }

      return categoryStats.Select(kv => new ChartData(kv.Key, kv.Value.Gross, kv.Value.Net)).ToList();
    });

    if (query.Loading) return Layout.Center().Height(Size.Units(60)).Add(Text.Label("Calculating Net vs Gross..."));

    var data = query.Value ?? [];

    if (data.Count == 0) return Layout.Center().Height(Size.Units(60)).Add(Text.Label("No category data available."));

    return new Card(
        Layout.Vertical().Gap(10).Padding(15)
            .Add(Layout.Center().Add(Text.H4("Net vs Gross by Category (SEK)")))
            .Add(Layout.Vertical().Height(Size.Units(60))
                .Add(data.ToBarChart()
                    .Dimension("Category", e => e.Category)
                    .Measure("Gross", e => e.Sum(f => f.Gross))
                    .Measure("Net", e => e.Sum(f => f.Net))
                    .Toolbox()
                )
            )
    ).Height(Size.Units(85));
  }
}
