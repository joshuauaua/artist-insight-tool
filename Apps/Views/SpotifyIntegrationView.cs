using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using ArtistInsightTool.Apps.Views.Spotify;

namespace ArtistInsightTool.Apps.Views;

public class SpotifyIntegrationView : ViewBase
{
  public override object? Build()
  {
    var isConnected = UseState(false);
    var logs = UseState<List<string>>([]);
    var streamData = UseState<List<SpotifyStreamData>>([]);
    var factory = UseService<ArtistInsightToolContextFactory>();

    Func<string, Task> Log = (msg) =>
    {
      logs.Set(l => [.. l, $"{DateTime.Now:HH:mm:ss}: {msg}"]);
      return Task.CompletedTask;
    };

    Func<Task> SyncStreams = async () =>
    {
      await Log("Starting Stream Sync...");
      await Task.Delay(500);

      await using var db = factory.CreateDbContext();

      // Find or create "Spotify" source
      var source = await db.RevenueSources.FirstOrDefaultAsync(s => s.DescriptionText == "Spotify");
      if (source == null)
      {
        source = new RevenueSource { DescriptionText = "Spotify" };
        db.RevenueSources.Add(source);
        await db.SaveChangesAsync();
      }

      var tracks = await db.Tracks.Include(t => t.Artist).ToListAsync();
      if (tracks.Count == 0)
      {
        await Log("No tracks found to sync.");
        return;
      }

      var random = new Random();
      int syncedCount = 0;

      // Simulate syncing for a few random tracks to avoid spamming too much data at once
      var trackSelection = tracks.OrderBy(x => random.Next()).Take(3).ToList();

      foreach (var track in trackSelection)
      {
        var streamCount = random.Next(1000, 50000);
        var revenue = streamCount * 0.004m; // Approx $0.004 per stream

        var entry = new RevenueEntry
        {
          RevenueDate = DateTime.Now,
          Amount = revenue,
          Description = $"{streamCount:N0} Streams for '{track.Title}'",
          SourceId = source.Id,
          ArtistId = track.ArtistId,
          TrackId = track.Id,
          Integration = "Spotify"
        };

        db.RevenueEntries.Add(entry);
        syncedCount++;
        await Log($"Synced {streamData.Value.Count + syncedCount}: {streamCount:N0} streams for {track.Title} (+${revenue:F2})");

        // Add to local table state
        string[] countries = { "USA", "UK", "Germany", "Japan", "Brazil", "Australia" };
        var country = countries[random.Next(countries.Length)];

        streamData.Set(s => [.. s, new SpotifyStreamData(
            track.Title,
            streamCount,
            revenue,
            country,
            DateTime.Now
        )]);
      }

      await db.SaveChangesAsync();
      await Log($"Sync Complete. Added revenue for {syncedCount} tracks.");
    };

    if (!isConnected.Value)
    {
      return Layout.Vertical()
          .Align(Align.Center)
          .Gap(20)
          .Padding(50)
          .Add(new Icon(Icons.Music).Size(48))
          .Add(Layout.Vertical().Gap(5).Align(Align.Center)
              .Add("Connect to Spotify")
              .Add("Import stream counts and calculate revenue")
          )
          .Add(new Button("Connect Account", async () =>
          {
            await Log("Redirecting to Spotify OAuth...");
            await Task.Delay(1500);
            isConnected.Set(true);
            await Log("Spotify Connected!");
          }));
    }

    return Layout.Vertical()
        .Gap(20)
        .Padding(20)
        .Add(new Card(
            Layout.Horizontal().Align(Align.Center).Gap(15)
                .Add(new Icon(Icons.Check).Size(24))
                .Add(Layout.Vertical().Gap(5)
                    .Add("Spotify Account Connected")
                    .Add("Ready to sync daily stream data")
                )
                .Add(Layout.Horizontal().Width(Size.Full()).Align(Align.Right)
                    .Add(new Button("Disconnect", () => isConnected.Set(false)).Variant(ButtonVariant.Outline))
                )
        ))
        .Add(new Card(
            Layout.Vertical().Gap(15)
                .Add("Actions")
                .Add(new Button("Sync Stream Data Now", async () => await SyncStreams()))
        ).Title("Data Synchronization"))
        .Add(new Card(
            Layout.Vertical().Gap(10)
                .Add("Sync Log")
                .Add(Layout.Vertical().Gap(5)
                    .Add(logs.Value.Select(l => l.ToString()).ToArray())
                )
        ))
        .Add(new Card(
            Layout.Vertical().Gap(10)
                .Add("Recent Stream Batches")
                .Add(SpotifyDataTable.Create(streamData.Value))
        ));
  }
}
