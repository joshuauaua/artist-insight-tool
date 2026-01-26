using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using ArtistInsightTool.Apps.Services;
using Ivy.Hooks;
using System.Linq;
using System.Collections.Generic;
using System;

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
    var client = UseService<IClientProvider>();

    // Tabs State
    var selectedTab = UseState(0);
    var tabs = new[] { "Overview", "Analytics", "Activity" };

    // Common Data Queries
    var assetsQuery = UseQuery("dashboard_assets", async (ct) => await service.GetAssetsAsync());
    var revenueQuery = UseQuery("dashboard_total_revenue", async (ct) => await service.GetTotalRevenueAsync(DateTime.Now.AddYears(-10), DateTime.Now));
    var activityQuery = UseQuery("dashboard_activity", async (ct) => await service.GetRevenueEntriesAsync());

    var assets = assetsQuery.Value ?? [];
    var totalRevenue = revenueQuery.Value;
    var activities = activityQuery.Value ?? [];

    var headerCard = new Card(
        Layout.Vertical().Gap(10)
            .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                 .Add(Text.H1("Dashboard"))
                 .Add(new Spacer().Width(Size.Fraction(1)))
                 .Add(new DropDownMenu(
                         DropDownMenu.DefaultSelectHandler(),
                         new Button("Actions").Variant(ButtonVariant.Outline)
                     )
                     | MenuItem.Default("Refresh All").HandleSelect(() =>
                     {
                       assetsQuery.Mutator.Revalidate();
                       revenueQuery.Mutator.Revalidate();
                       activityQuery.Mutator.Revalidate();
                       client.Toast("Dashboard data refreshed");
                     })
                 )
            )
            .Add(Layout.Horizontal().Gap(2).Padding(0, 0, 10, 0)
                | tabs.Select((tab, index) =>
                    new Button(tab, _ => selectedTab.Set(index))
                        .Variant(selectedTab.Value == index ? ButtonVariant.Primary : ButtonVariant.Ghost)
                )
            )
    );

    // --- Tab Views ---

    object OverviewTab() => Layout.Vertical().Gap(20)
        .Add(Layout.Grid().Columns(3).Gap(20)
            .Add(new Card(Layout.Center().Add(Text.H2(assets.Count.ToString()))).Title("Total Assets").Description("Number of tracked assets"))
            .Add(new Card(Layout.Center().Add(Text.H2(totalRevenue?.Value ?? "$0.00"))).Title("Total Revenue").Description("Accumulated revenue across all sources"))
            .Add(new Card(Layout.Center().Add(Text.H2(activities.Count.ToString()))).Title("Data Imports").Description("Number of successful data imports")))
        .Add(new Card(Text.P("Welcome to your dashboard overview. Switch to the Analytics tab for detailed breakdowns.")).Title("Quick Start"));

    object AnalyticsTab()
    {
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

      // Filtering for Secondary Chart (Asset breakdown for selected category)
      var assetBreakdown = assets
          .Where(a => (a.Category ?? "Uncategorized") == selectedCategory.Value)
          .Select(a => new AssetRevenueItem(
            a.Name,
            (double)a.AmountGenerated
          ))
          .ToList();

      if (assets.Count == 0) return Layout.Center().Add(Text.Label("No asset data available to display."));

      return Layout.Vertical().Gap(20)
          .Add(Layout.Horizontal().Gap(20).Width(Size.Full())
              .Add(new Card(
                  categoryData.ToPieChart(
                      e => e.Category,
                      g => g.Sum(f => f.Amount))
              ).Title("Revenue by Category").Description("Total revenue across all categories").Width(Size.Fraction(0.5f)))

              .Add(new Card(
                  assetBreakdown.ToPieChart(
                      e => e.Asset,
                      g => g.Sum(f => f.Amount))
              ).Title($"{selectedCategory.Value} Breakdown").Description($"Revenue breakdown by asset in {selectedCategory.Value}").Width(Size.Fraction(0.5f)))
          )
          .Add(Layout.Horizontal().Align(Align.Center).Gap(10)
              .Add(Text.Label("Select Category to Drill Down"))
              .Add(selectedCategory.ToSelectInput(categories.ToOptions()).Width(300)));
    }

    object ActivityTab()
    {
      if (activities.Count == 0) return Layout.Center().Add(Text.Label("No recent activity found."));

      return new Card(
          activities.Select(a => new
          {
            Description = a.Description ?? "Untitled",
            Source = a.Integration ?? "Manual",
            Amount = a.Amount.ToString("C"),
            Date = a.RevenueDate.ToShortDateString()
          }).Take(10).ToArray().ToTable()
      ).Title("Recent Data Imports").Description("Latest revenue entries added to the system.");
    }

    var content = Layout.Vertical().Height(Size.Full()).Padding(20);

    if (assetsQuery.Loading || revenueQuery.Loading || activityQuery.Loading)
    {
      content.Add(Layout.Center().Add(Text.Label("Loading dashboard data...")));
    }
    else
    {
      content.Add(selectedTab.Value switch
      {
        0 => OverviewTab(),
        1 => AnalyticsTab(),
        2 => ActivityTab(),
        _ => OverviewTab()
      });
    }

    return new HeaderLayout(headerCard, content);
  }
}
