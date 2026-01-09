using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.CreditCard, path: ["RevenueSources"])]
public class RevenueSourcesApp : ViewBase
{
  public override object? Build()
  {
    return this.UseBlades(() => new RevenueSourceListBlade(), "Revenue Sources");
  }
}
