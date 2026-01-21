using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;

namespace ArtistInsightTool.Apps.Dashboard;

[App(icon: Icons.LayoutDashboard, title: "Dashboard", path: ["Main"])]
public class DashboardApp : ViewBase
{
  public override object Build()
  {
    var headerContent = Layout.Vertical()
        .Width(Size.Full())
        .Height(Size.Fit())
        .Gap(10)
        .Padding(20, 20, 20, 5)
        .Add(Layout.Horizontal().Width(Size.Full()).Height(Size.Fit()).Align(Align.Center)
             .Add("Dashboard")
             .Add(new Spacer().Width(Size.Fraction(1)))
             .Add(new DropDownMenu(
                     DropDownMenu.DefaultSelectHandler(),
                     new Button("Actions").Variant(ButtonVariant.Outline)
                 )
                 | MenuItem.Default("Refresh")
             )
        )
        .Add(Layout.Horizontal().Width(Size.Full()).Height(Size.Fit()).Gap(10)
             .Add(new TextInput().Placeholder("Search dashboard...").Width(300))
        );

    return new Fragment(
        Layout.Vertical()
            .Height(Size.Full())
            .Gap(0)
            .Add(headerContent)
            .Add(Layout.Vertical().Height(Size.Fraction(1)).Padding(20)
                 .Add(Text.Label("Dashboard Placeholder"))
            )
    );
  }
}
