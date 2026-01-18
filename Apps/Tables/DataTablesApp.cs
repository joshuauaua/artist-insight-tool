using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.IO;
using System.Text; // Added for StringBuilder
using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps.Tables;

[App(icon: Icons.Database, title: "Data Tables", path: ["Tables"])]
public class DataTablesApp : ViewBase
{
  public record TableItem(int RealId, string Id, string Name, string AnnexedTo, string LinkedTo, string Date);

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
            .OrderBy(e => e.CreatedAt)
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
            void AddItem(string title) => items.Add(new TableItem(entry.Id, $"DT{index++:D3}", title, entry.Description ?? "-", "-", entry.UpdatedAt.ToShortDateString()));

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

        items.Reverse(); // Show newest first
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
    }, [EffectTrigger.AfterInit(), refresh]);

    var selectedIds = UseState<HashSet<string>>([]);
    var searchQuery = UseState("");

    // Filter items based on search
    var filteredItems = tables.Value;
    if (!string.IsNullOrWhiteSpace(searchQuery.Value))
    {
      var q = searchQuery.Value.ToLowerInvariant();
      filteredItems = filteredItems.Where(t =>
          t.Name.ToLowerInvariant().Contains(q) ||
          t.Id.ToLowerInvariant().Contains(q)
      ).ToList();
    }

    bool allSelected = filteredItems.Count > 0 && selectedIds.Value.Count == filteredItems.Count;

    var mappingEntryId = UseState<int?>(initialValue: null);

    if (mappingEntryId.Value.HasValue)
    {
      return new HeaderMappingSheet(mappingEntryId.Value.Value, () => mappingEntryId.Set((int?)null));
    }

    var headerContent = Layout.Vertical()
       .Width(Size.Full())
       .Height(Size.Fit())
       .Gap(10)
       .Padding(20, 20, 20, 5)
       .Add(Layout.Horizontal()
            .Width(Size.Full())
            .Height(Size.Fit())
            .Align(Align.Center)
            .Add("Data Tables")
            .Add(new Spacer().Width(Size.Fraction(1)))
            .Add(new Button("Import Data", () => Console.WriteLine("User clicked Import"))
                .Icon(Icons.FileUp)
                .Variant(ButtonVariant.Primary)
            )
       )
       .Add(Layout.Horizontal()
           .Width(Size.Full())
           .Height(Size.Fit())
           .Gap(10)
           .Add(searchQuery.ToTextInput().Placeholder("Search tables...").Width(300))
       );

    return new Fragment(
        Layout.Vertical()
            .Height(Size.Full())
            .Gap(0)
            .Add(headerContent)
            .Add(isLoading.Value
                ? Layout.Center().Padding(50).Add(Text.Label("Loading..."))
                : Layout.Vertical()
                    .Height(Size.Fraction(1))
                    .Padding(20, 0, 20, 50)
                    .Add(filteredItems.Count > 0
                        ? filteredItems.Select(t => new
                        {
                          IdButton = new Button(t.Id, () => { }).Variant(ButtonVariant.Ghost),
                          t.Name,
                          t.AnnexedTo,
                          t.LinkedTo,
                          t.Date
                        }).ToArray().ToTable()
                            .Width(Size.Full())
                            // Header checkbox for Select All
                            .Add(x => x.IdButton)
                            .Add(x => x.Name)
                            .Add(x => x.AnnexedTo)
                            .Add(x => x.LinkedTo)
                            .Add(x => x.Date)
                            .Header(x => x.IdButton, "ID")
                            .Header(x => x.Name, "Name")
                            .Header(x => x.AnnexedTo, "Annexed To")
                            .Header(x => x.LinkedTo, "Linked To")
                            .Header(x => x.Date, "Uploaded")
                        : Text.Muted("No data tables found.")
                    )
            )
    );
  }
}
