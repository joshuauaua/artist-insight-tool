using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;

namespace ArtistInsightTool.Apps.Views;

public class CsvImportView : ViewBase
{
  public override object? Build()
  {
    var csvContent = UseState("");
    var logs = UseState<List<string>>([]);
    var factory = UseService<ArtistInsightToolContextFactory>();

    Func<string, Task> Log = async (msg) =>
    {
      logs.Set(l => [.. l, $"{DateTime.Now:HH:mm:ss}: {msg}"]);
    };

    Func<Task> ImportCsv = async () =>
    {
      if (string.IsNullOrWhiteSpace(csvContent.Value))
      {
        await Log("Error: CSV content is empty.");
        return;
      }

      var lines = csvContent.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      await Log($"Found {lines.Length} lines. Processing...");

      await using var db = factory.CreateDbContext();
      int successCount = 0;

      foreach (var line in lines)
      {
        // Skip header if it looks like one
        if (line.StartsWith("Date,", StringComparison.OrdinalIgnoreCase)) continue;

        var parts = line.Split(',');
        if (parts.Length < 5)
        {
          await Log($"Skipping invalid line (less than 5 columns): {line}");
          continue;
        }

        try
        {
          // Parse columns: Date, Amount, Description, Source, Artist
          var dateStr = parts[0].Trim();
          var amountStr = parts[1].Trim();
          var description = parts[2].Trim();
          var sourceName = parts[3].Trim();
          var artistName = parts[4].Trim();

          if (!DateTime.TryParse(dateStr, out var date))
          {
            await Log($"Invalid date format: {dateStr}");
            continue;
          }

          if (!decimal.TryParse(amountStr, out var amount))
          {
            await Log($"Invalid amount format: {amountStr}");
            continue;
          }

          // Handle Source
          var source = await db.RevenueSources.FirstOrDefaultAsync(s => s.DescriptionText == sourceName);
          if (source == null)
          {
            source = new RevenueSource { DescriptionText = sourceName };
            db.RevenueSources.Add(source);
            await db.SaveChangesAsync(); // Save to get Id
          }

          // Handle Artist
          var artist = await db.Artists.FirstOrDefaultAsync(a => a.Name == artistName);
          if (artist == null)
          {
            artist = new Artist { Name = artistName };
            db.Artists.Add(artist);
            await db.SaveChangesAsync(); // Save to get Id
          }

          var entry = new RevenueEntry
          {
            RevenueDate = date,
            Amount = amount,
            Description = description,
            SourceId = source.Id,
            ArtistId = artist.Id
          };

          db.RevenueEntries.Add(entry);
          successCount++;
        }
        catch (Exception ex)
        {
          await Log($"Error processing line '{line}': {ex.Message}");
        }
      }

      await db.SaveChangesAsync();
      await Log($"Import completed. Successfully imported {successCount} entries.");
      csvContent.Set(""); // Clear input on success setup? Maybe keep it for reference.
    };

    return Layout.Vertical()
        .Gap(20)
        .Padding(20)
        .Add(new Card(
            Layout.Vertical().Gap(15)
                .Add("CSV Import")
                .Add("Paste your revenue data below. Format: Date, Amount, Description, Source, Artist")
                .Add(csvContent.ToTextInput().Placeholder("2025-01-01, 100.50, Merch Sale, Bandcamp, My Band..."))
                .Add(new Button("Import Data", async () => await ImportCsv()))
        ))
        .Add(new Card(
            Layout.Vertical().Gap(10)
                .Add("Import Log")
                .Add(Layout.Vertical().Gap(5)
                    .Add(logs.Value.Select(l => l.ToString()).ToArray())
                )
        ));
  }
}
