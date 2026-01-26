using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using ArtistInsightTool.Apps.Services;
using Ivy.Hooks;
using System.Linq;
using System.Collections.Generic;

namespace ArtistInsightTool.Apps.Dashboard;

[App(icon: Icons.LayoutDashboard, title: "Dashboard", path: ["Main"])]
public class DashboardApp : ViewBase
{
  // Define concrete records to avoid Hot Reload issues with anonymous types
  public record CategoryRevenue(string Category, double Amount);
  public record AssetRevenueItem(string Asset, double Amount);

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var assetsQuery = UseQuery("dashboard_assets", async (ct) => await service.GetAssetsAsync());

    // States
    var searchQuery = UseState("");
    var assets = assetsQuery.Value ?? [];

    // Grouping for Primary Chart (Category)
    var categoryData = assets
        .GroupBy(a => a.Category ?? "Uncategorized")
        .Select(g => new CategoryRevenue(
          g.Key,
          (double)g.Sum(a => a.AmountGenerated)
        ))
        .ToList();

    var categories = categoryData.Select(c => c.Category).OrderBy(c => c).ToList();
    var selectedCategory = UseState(categories.FirstOrDefault() ?? "Uncategorized");

    var headerCard = new Card(
        Layout.Vertical().Gap(10)
            .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                 .Add(Text.H1("Dashboard"))
                 .Add(new Spacer().Width(Size.Fraction(1)))
                 .Add(new DropDownMenu(
                         DropDownMenu.DefaultSelectHandler(),
                         new Button("Actions").Variant(ButtonVariant.Outline)
                     )
                     | MenuItem.Default("Refresh").HandleSelect(() => assetsQuery.Mutator.Revalidate())
                 )
            )
            .Add(Layout.Horizontal().Width(Size.Full()).Gap(10)
                 .Add(searchQuery.ToTextInput().Placeholder("Search dashboard...").Width(300))
            )
    );

    // Filtering for Secondary Chart (Asset breakdown for selected category)
    var assetBreakdown = assets
        .Where(a => (a.Category ?? "Uncategorized") == selectedCategory.Value)
        .Select(a => new AssetRevenueItem(
          a.Name,
          (double)a.AmountGenerated
        ))
        .ToList();

    var content = Layout.Vertical().Height(Size.Full()).Padding(20).Gap(20);

    if (assetsQuery.Loading)
    {
      content.Add(Layout.Center().Add(Text.Label("Loading dashboard data...")));
    }
    else if (assets.Count == 0)
    {
      content.Add(Layout.Center().Add(Text.Label("No asset data available to display.")));
    }
    else
    {
      content.Add(
          new Card(
              Layout.Vertical().Gap(10).Padding(2)
                .Add(Layout.Horizontal().Gap(20).Width(Size.Full())
                    .Add(Layout.Vertical().Gap(10).Width(Size.Fraction(0.5f)).Align(Align.Center)
                        .Add(Text.H5("Total Revenue by Category"))
                        .Add(categoryData.ToPieChart(
                            e => e.Category,
                            e => e.Sum(f => f.Amount))))

                    .Add(Layout.Vertical().Gap(10).Width(Size.Fraction(0.5f)).Align(Align.Center)
                        .Add(Text.H5($"{selectedCategory.Value} Asset Breakdown"))
                        .Add(assetBreakdown.ToPieChart(
                            e => e.Asset,
                            e => e.Sum(f => f.Amount))))
                )
                .Add(Layout.Vertical().Gap(10).Align(Align.Center)
                    .Add(Text.Muted("Select Category to Drill Down").Small())
                    .Add(selectedCategory.ToSelectInput(categories.ToOptions()).Width(100)))
          ).Title("Revenue Drilldown").Description("Analyze revenue by category and deep-dive into individual assets.")
      );
    }

    return new HeaderLayout(headerCard, content);
  }
}
