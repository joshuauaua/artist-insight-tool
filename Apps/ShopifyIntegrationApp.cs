using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.ShoppingBag, path: ["Integrations", "Shopify"])]
public class ShopifyIntegrationApp : ViewBase
{
  public override object? Build()
  {
    return new ShopifyIntegrationView();
  }
}
