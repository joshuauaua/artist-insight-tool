using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using ArtistInsightTool.Apps.Views.Shopify;

namespace ArtistInsightTool.Apps;

public class ShopifyIntegrationView : ViewBase
{
  public override object? Build()
  {
    var isConnected = UseState(false);
    var logs = UseState<List<string>>([]);
    var sales = UseState<List<ShopifySale>>([]);
    var factory = UseService<ArtistInsightToolContextFactory>();

    Func<string, Task> Log = (msg) =>
    {
      logs.Set(l => [.. l, $"{DateTime.Now:HH:mm:ss}: {msg}"]);
      return Task.CompletedTask;
    };

    Func<Task> SimulateSale = async () =>
    {
      await Log("Received webhook: orders/create");
      await Task.Delay(500);

      var amount = new Random().Next(20, 100);
      var product = "Limited Edition Vinyl";

      await Log($"Processing order for {product} (${amount})");

      await using var db = factory.CreateDbContext();

      // Find or create "Merch" source
      var source = await db.RevenueSources.FirstOrDefaultAsync(s => s.DescriptionText == "Merch");
      if (source == null)
      {
        source = new RevenueSource { DescriptionText = "Merch" };
        db.RevenueSources.Add(source);
        await db.SaveChangesAsync();
      }

      // Need a valid ArtistId
      var artist = await db.Artists.FirstOrDefaultAsync();
      if (artist == null)
      {
        await Log("Error: No artists found in DB. Cannot create entry.");
        return;
      }

      var entry = new RevenueEntry
      {
        RevenueDate = DateTime.Now,
        Amount = amount,
        Description = $"Shopify Sale: {product}",
        SourceId = source.Id,
        ArtistId = artist.Id,
        Integration = "Shopify"
      };

      await Log($"Debug: Saving Entry. SourceId: {source.Id}, ArtistId: {artist.Id}");
      db.RevenueEntries.Add(entry);
      await db.SaveChangesAsync();

      sales.Set(s => [.. s, new ShopifySale(
          Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
          DateTime.Now,
          "Customer " + new Random().Next(1000, 9999),
          product,
          amount,
          "Paid"
      )]);

      await Log("Sale registered in Revenue Table & Shopify Table");
    };


    if (!isConnected.Value)
    {
      return Layout.Vertical()
          .Align(Align.Center)
          .Gap(20)
          .Padding(50)
          .Add(new Icon(Icons.ShoppingBag).Size(48)) // Removed Size if not present or check API. keeping generic
          .Add(Layout.Vertical().Gap(5).Align(Align.Center)
              .Add("Connect to Shopify")
              .Add("Sync your sales data automatically")
          )
          .Add(new Button("Connect Store", async () =>
          {
            await Log("Initiating OAuth flow...");
            await Task.Delay(1500);
            isConnected.Set(true);
            await Log("Connected successfully!");
          }));
    }

    return Layout.Vertical()
        .Gap(20)
        .Padding(20)
        .Add(new Card(
            Layout.Horizontal().Align(Align.Center).Gap(15)
                .Add(new Icon(Icons.Check).Size(24)) // Changed CheckCircle to Check
                .Add(Layout.Vertical().Gap(5)
                    .Add("Shopify Store Connected")
                    .Add("Syncing orders in real-time")
                )
                .Add(Layout.Horizontal().Width(Size.Full()).Align(Align.Right)
                    .Add(new Button("Disconnect", () => isConnected.Set(false)).Variant(ButtonVariant.Outline))
                )
        ))
        .Add(new Card(
            Layout.Vertical().Gap(15)
                .Add("Trigger Test Events")
                .Add(new Button("Simulate Incoming Sale", async () => await SimulateSale()))
        ).Title("Debug Tools"))
        .Add(new Card(
            Layout.Vertical().Gap(10)
                .Add("Activity Log")
                .Add(Layout.Vertical().Gap(5)
                    .Add(logs.Value.Select(l => l.ToString()).ToArray())
                )
        ))
        .Add(new Card(
            Layout.Vertical().Gap(10)
                .Add("Recent Sales")
                .Add(ShopifyDataTable.Create(sales.Value))
        ));
  }
}
