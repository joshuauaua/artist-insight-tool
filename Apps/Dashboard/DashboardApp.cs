using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;

namespace ArtistInsightTool.Apps.Dashboard;

[App(icon: Icons.LayoutDashboard, title: "Dashboard", path: ["Main"])]
public class DashboardApp : ViewBase
{
  public override object Build()
  {
    var headerCard = new Card(
        Layout.Vertical().Gap(10)
            .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                 .Add(Text.H4("Dashboard"))
                 .Add(new Spacer().Width(Size.Fraction(1)))
                 .Add(new DropDownMenu(
                         DropDownMenu.DefaultSelectHandler(),
                         new Button("Actions").Variant(ButtonVariant.Outline)
                     )
                     | MenuItem.Default("Refresh")
                 )
            )
            .Add(Layout.Horizontal().Width(Size.Full()).Gap(10)
                 .Add(new TextInput().Placeholder("Search dashboard...").Width(300))
            )
    );

    var content = Layout.Vertical().Height(Size.Full()).Padding(20)
                .Add(Text.Label("Dashboard Placeholder"));

    return new HeaderLayout(headerCard, content);
  }
}
