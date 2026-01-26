using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using ArtistInsightTool.Apps.Services;
using Ivy.Hooks;
using System.Linq;

namespace ArtistInsightTool.Apps.Dashboard;

[App(icon: Icons.LayoutDashboard, title: "Dashboard", path: ["Main"])]
public class DashboardApp : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var assetsQuery = UseQuery("dashboard_assets", async (ct) => await service.GetAssetsAsync());

    var headerCard = new Card(
        Layout.Vertical().Gap(10)
            .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                 .Add(Text.H4("Dashboard"))
                 .Add(new Spacer().Width(Size.Fraction(1)))
                 .Add(new DropDownMenu(
                         DropDownMenu.DefaultSelectHandler(),
                         new Button("Actions").Variant(ButtonVariant.Outline)
                     )
                     | MenuItem.Default("Refresh").HandleSelect(() => assetsQuery.Mutator.Revalidate())
                 )
            )
            .Add(Layout.Horizontal().Width(Size.Full()).Gap(10)
                 .Add(new TextInput().Placeholder("Search dashboard...").Width(300))
            )
    );

    var assets = assetsQuery.Value ?? [];
    var categoryData = assets
        .GroupBy(a => a.Category ?? "Uncategorized")
        .Select(g => new
        {
          Category = g.Key,
          Amount = (double)g.Sum(a => a.AmountGenerated)
        })
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
      content.Add(Layout.Grid().Columns(2).Gap(20).Width(Size.Full())
          | new Card(
              categoryData.ToPieChart(
                  e => e.Category,
                  e => e.Sum(f => f.Amount))
          ).Title("Revenue by Category").Description("Total amount generated per asset category").Height(Size.Units(100))
      );
    }

    return new HeaderLayout(headerCard, content);
  }
}
