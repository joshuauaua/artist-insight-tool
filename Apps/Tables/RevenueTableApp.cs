namespace ArtistInsightTool.Apps.Tables;

// [App(icon: Icons.Table, path: ["Tables"])]
public class RevenueTableApp : ViewBase
{
  public override object? Build()
  {
    return new RevenueTableView();
  }
}
