using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.DollarSign, path: ["Apps"])]
public class RevenueEntriesApp : ViewBase
{
  public override object? Build()
  {
    return this.UseBlades(() => new RevenueEntryListBlade(), "Search");
  }
}
