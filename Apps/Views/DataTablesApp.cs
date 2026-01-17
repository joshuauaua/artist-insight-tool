using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.IO;
using System.Text; // Added for StringBuilder

namespace ArtistInsightTool.Apps.Views;

[App(icon: Icons.Database, title: "Data Tables", path: ["Pages"])]
public class DataTablesApp : ViewBase
{
  public record TableItem(string Id, string Name, string AnnexedTo, string LinkedTo, string Date);

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var refresh = UseState(0);
    var tables = UseState<List<TableItem>>([]);
    var isLoading = UseState(true);
    var debug = UseState<string>("");

    UseEffect(async () =>
    {
      isLoading.Set(true);
      try
      {
        // Small delay to ensure UI renders loading state first
        await Task.Delay(10);

        await using var db = factory.CreateDbContext();
        var entries = await db.RevenueEntries
            .Include(e => e.AssetRevenues).ThenInclude(ar => ar.Asset)
            .Where(e => e.JsonData != null && e.JsonData != "")
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        var items = new List<TableItem>();
        int index = 1;

        foreach (var entry in entries)
        {
          try
          {
            if (string.IsNullOrWhiteSpace(entry.JsonData)) continue;
            using var doc = JsonDocument.Parse(entry.JsonData);
            var root = doc.RootElement;

            // Logic to parse items... (Compact helper to keep code short)
            void AddItem(string title) => items.Add(new TableItem($"DT{index++:D3}", title, entry.Description ?? "-", "-", entry.UpdatedAt.ToShortDateString()));

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
              var first = root[0];
              bool isObj = first.ValueKind == JsonValueKind.Object;
              bool hasFile = isObj && (first.TryGetProperty("FileName", out _) || first.TryGetProperty("fileName", out _));

              if (isObj && hasFile)
              {
                foreach (var el in root.EnumerateArray())
                {
                  string t = "Untitled";
                  if (el.TryGetProperty("Title", out var p) || el.TryGetProperty("title", out p)) t = p.GetString() ?? "Untitled";
                  AddItem(t);
                }
              }
              else AddItem("Legacy Data");
            }
            else if (root.ValueKind == JsonValueKind.Object) AddItem("Single Sheet");
          }
          catch { }
        }

        tables.Set(items);
      }
      catch (Exception ex)
      {
        debug.Set($"Error: {ex.Message}");
      }
      finally
      {
        isLoading.Set(false);
      }
    }, [refresh]);

    return Layout.Vertical()
        .Padding(20)
        .Gap(20)
        .Add(Text.H3("Data Tables"))
        .Add(Layout.Horizontal().Gap(10).Align(Align.Center)
            .Add(new Button("Refresh", () => refresh.Set(refresh.Value + 1)))
            .Add(isLoading.Value ? Text.Muted("Loading data...") : Text.Muted($"{tables.Value.Count} tables found"))
        )
        .Add(isLoading.Value
            ? Layout.Center().Padding(50).Add(Text.Label("Loading..."))
            : tables.Value.Count > 0
                ? tables.Value.ToArray().ToTable()
                    .Header(x => x.Id, "ID")
                    .Header(x => x.Name, "Name")
                    .Header(x => x.AnnexedTo, "Annexed To")
                    .Header(x => x.LinkedTo, "Linked To")
                    .Header(x => x.Date, "Uploaded")
                : Text.Muted("No data tables found.")
        );
  }
}
