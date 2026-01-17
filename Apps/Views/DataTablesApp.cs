using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ArtistInsightTool.Apps.Views;

[App(icon: Icons.Database, title: "Data Tables", path: ["Pages"])]
public class DataTablesApp : ViewBase
{
  record TableItem(string Id, string Name, string AnnexedTo, string LinkedTo, string Date);

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var refresh = UseState(0);
    var tables = UseState<List<TableItem>>([]);
    var debug = UseState<List<string>>([]);
    var debugLogs = new List<string>();

    UseEffect(async () =>
    {
      await using var db = factory.CreateDbContext();
      var entries = await db.RevenueEntries
          .Include(e => e.AssetRevenues).ThenInclude(ar => ar.Asset)
          .Where(e => e.JsonData != null && e.JsonData != "")
          .OrderByDescending(e => e.CreatedAt)
          .ToListAsync();

      var items = new List<TableItem>();
      int index = 1;

      // Flatten the list first to assign IDs sequentially based on date descending
      // Actually, if we want IDs to be somewhat stable per "session" or query, we just count.
      // Or we can reverse the index if we want "DT001" to be the oldest.
      // Let's assume user wants "DT001" to be the first one in the list (newest or oldest? usually IDs are chronological).
      // Let's make DT001 the oldest. So we need to process in temporal order or assign IDs then reversing.
      // But query is OrderByDescending(CreatedAt).
      // Let's just use descending index for now or simple 1..N. The user prompt "DT001 format" usually implies a unique ID.
      // I will generate them based on the total count downwards? Or just 1..N in the displayed list.
      // Let's do 1..N in the displayed list for simplicity as "View ID".

      debugLogs.Add($"Found {entries.Count} entries with potential data.");
      Console.WriteLine($"DataTablesApp: Found {entries.Count} entries with potential data.");

      foreach (var entry in entries)
      {
        try
        {
          if (string.IsNullOrWhiteSpace(entry.JsonData))
          {
            debugLogs.Add($"Entry {entry.Id}: JsonData is empty/null");
            Console.WriteLine($"DataTablesApp: Entry {entry.Id} has empty JsonData");
            continue;
          }

          // DEBUG: Dump the first few characters
          debugLogs.Add($"Entry {entry.Id} Raw Start: {entry.JsonData.Substring(0, Math.Min(100, entry.JsonData.Length))}");

          using var doc = JsonDocument.Parse(entry.JsonData);
          var root = doc.RootElement;

          debugLogs.Add($"Entry {entry.Id} Root Kind: {root.ValueKind}");

          if (root.ValueKind == JsonValueKind.Array)
          {
            int arrayLen = root.GetArrayLength();
            debugLogs.Add($"Entry {entry.Id} Array Length: {arrayLen}");

            if (arrayLen > 0)
            {
              var first = root[0];
              debugLogs.Add($"Entry {entry.Id} First Item Kind: {first.ValueKind}");

              // Try casing check
              var hasFileName = first.TryGetProperty("FileName", out _) || first.TryGetProperty("fileName", out _);
              debugLogs.Add($"Entry {entry.Id} Has 'FileName' (or camel): {hasFileName}");

              if (first.ValueKind == JsonValueKind.Object && hasFileName)
              {
                foreach (var element in root.EnumerateArray())
                {
                  // Try both cases for Title
                  string title = "Unknown";
                  if (element.TryGetProperty("Title", out var t)) title = t.GetString();
                  else if (element.TryGetProperty("title", out t)) title = t.GetString();

                  items.Add(new TableItem(
                      $"DT{index:D3}",
                      title ?? "Untitled",
                      entry.Description ?? "No Description",
                      "-", // Ignoring links for debug simplicity
                      entry.UpdatedAt.ToShortDateString()
                  ));
                  index++;
                }
              }
              else
              {
                debugLogs.Add($"Entry {entry.Id}: Fallback to legacy row mode.");
                items.Add(new TableItem(
                     $"DT{index:D3}",
                    "Legacy Data",
                    entry.Description ?? "No Description",
                    "-",
                    entry.UpdatedAt.ToShortDateString()
                ));
                index++;
              }
            }
          }
          else if (root.ValueKind == JsonValueKind.Object)
          {
            debugLogs.Add($"Entry {entry.Id}: Object mode (Single Sheet)");
            items.Add(new TableItem(
                 $"DT{index:D3}",
                "Single Sheet",
                entry.Description ?? "No Description",
                "-",
                entry.UpdatedAt.ToShortDateString()
            ));
            index++;
          }
        }
        catch (Exception ex)
        {
          var msg = $"Error Entry {entry.Id}: {ex.Message}";
          debugLogs.Add(msg);
          Console.WriteLine(msg);
        }
      }

      if (items.Count == 0)
      {
        debugLogs.Add("No items generated from any entries.");
        Console.WriteLine("DataTablesApp: No items generated.");
      }
      else
      {
        Console.WriteLine($"DataTablesApp: Generated {items.Count} items.");
      }

      debug.Set(debugLogs);
      tables.Set(items);
      return null;
    }, [refresh]);

    return Layout.Vertical()
        .Padding(20)
        .Gap(20)
        .Add(Text.H3("Data Tables"))
        .Add(new Card(
            Layout.Vertical().Gap(5).Padding(10)
            .Add(Text.Label("Debug Info"))
            .Add(new Markdown($"```\n{string.Join("\n", debug.Value)}\n```"))
        ))
        .Add(tables.Value.Select(t => new
        {
          t.Id,
          t.Name,
          t.AnnexedTo,
          t.LinkedTo,
          t.Date
        }).ToArray().ToTable()
            .Header(x => x.Id, "ID")
            .Header(x => x.Name, "Name")
            .Header(x => x.AnnexedTo, "Annexed To")
            .Header(x => x.LinkedTo, "Linked To")
            .Header(x => x.Date, "Uploaded")
        );
  }
}
