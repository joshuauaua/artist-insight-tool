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
        ))
        .Add(new ImportArtistCard(factory, Log));
  }
}

public class ImportArtistCard(ArtistInsightToolContextFactory factory, Func<string, Task> log) : ViewBase
{
  public override object Build()
  {
    var artistId = UseState(""); // Default from prompt? "0TnOYISbd1XYRBk9myaseg"
    var token = UseState("");
    var isImporting = UseState(false);

    return new Card(
        Layout.Vertical().Gap(15)
            .Add("Import Artist from Spotify")
            .Add(Layout.Vertical().Gap(5)
                .Add(Text.Label("Spotify Artist ID"))
                .Add(artistId.ToTextInput().Placeholder("e.g. 0TnOYISbd1XYRBk9myaseg"))
            )
            .Add(Layout.Vertical().Gap(5)
                .Add(Text.Label("Bearer Token"))
                .Add(token.ToTextInput().Placeholder("Authorization: Bearer ..."))
            )
            .Add(new Button("Fetch & Import", async () =>
            {
              if (string.IsNullOrWhiteSpace(artistId.Value) || string.IsNullOrWhiteSpace(token.Value))
              {
                await log("Error: Artist ID and Token are required.");
                return;
              }

              isImporting.Set(true);
              await log($"Fetching artist {artistId.Value}...");

              try
              {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Value);

                var url = $"https://api.spotify.com/v1/artists/{artistId.Value}";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                  var err = await response.Content.ReadAsStringAsync();
                  await log($"Spotify API Error: {response.StatusCode} - {err}");
                  isImporting.Set(false);
                  return;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("name", out var nameProp))
                {
                  var artistName = nameProp.GetString();
                  if (!string.IsNullOrEmpty(artistName))
                  {
                    await log($"Found Arist: {artistName}. Saving...");
                    await using var db = factory.CreateDbContext();

                    var existing = await db.Artists.FirstOrDefaultAsync(a => a.Name == artistName);
                    if (existing == null)
                    {
                      db.Artists.Add(new Artist
                      {
                        Name = artistName,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                      });
                      await db.SaveChangesAsync();
                      await log($"Successfully imported artist: {artistName}");
                    }
                    else
                    {
                      await log($"Artist '{artistName}' already exists.");
                    }
                  }
                }
                else
                {
                  await log("Error: Could not parse 'name' from response.");
                }
              }
              catch (Exception ex)
              {
                await log($"Exception: {ex.Message}");
              }

              isImporting.Set(false);
            }).Variant(ButtonVariant.Primary).Disabled(isImporting.Value))
    );
  }
}

