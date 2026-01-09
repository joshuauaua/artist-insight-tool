using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Table, path: ["RevenueTable"])]
public class RevenueTableApp : ViewBase
{
  public override object? Build()
  {
    return new RevenueTableView();
  }
}
