using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.ShoppingBag, title: "Shopify Integration", path: ["Pages"])]
public class ShopifyIntegrationApp : ViewBase
{
  public override object? Build()
  {
    return new ShopifyIntegrationView();
  }
}
