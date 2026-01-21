using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.IO;
using System.Text; // Added for StringBuilder
using ArtistInsightTool.Apps.Views;
using ArtistInsightTool.Apps.Services;

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

    var service = UseService<ArtistInsightService>();
    var confirmDeleteId = UseState<int?>(() => null);
    var selectedTableId = UseState<int?>(() => null);

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

    var mappingEntryId = UseState<int?>(initialValue: null);

    // Sheets
    object? sheets = null;
    if (selectedTableId.Value != null)
    {
      sheets = new DataTableViewSheet(selectedTableId.Value.Value, () => selectedTableId.Set((int?)null));
    }
    else if (mappingEntryId.Value.HasValue)
    {
      // Keep existing logic for HeaderMappingSheet but wrap/assign if needed. 
      // The original code returned HeaderMappingSheet directly.
      // Let's integrate it into sheets as well.
      sheets = new HeaderMappingSheet(mappingEntryId.Value.Value, () => mappingEntryId.Set((int?)null));
    }

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
                          IdButton = new Button(t.Id, () => selectedTableId.Set(t.RealId)).Variant(ButtonVariant.Ghost),
                          t.Name,
                          t.AnnexedTo,
                          t.LinkedTo,
                          t.Date,
                          Actions = new Button("", () => confirmDeleteId.Set(t.RealId)).Icon(Icons.Trash).Variant(ButtonVariant.Ghost)
                        }).ToArray().ToTable()
                            .Width(Size.Full())
                            // Header checkbox for Select All
                            .Add(x => x.IdButton)
                            .Add(x => x.Name)
                            .Add(x => x.AnnexedTo)
                            .Add(x => x.LinkedTo)
                            .Add(x => x.Date)
                            .Add(x => x.Actions)
                            .Header(x => x.IdButton, "ID")
                            .Header(x => x.Name, "Name")
                            .Header(x => x.AnnexedTo, "Annexed To")
                            .Header(x => x.LinkedTo, "Linked To")
                            .Header(x => x.Date, "Uploaded")
                            .Header(x => x.Actions, "")
                        : Text.Muted("No data tables found.")
                    )
            ),
        confirmDeleteId.Value != null ? new Dialog(
            _ => confirmDeleteId.Set((int?)null),
            new DialogHeader("Confirm Deletion"),
            new DialogBody(Text.Label("Are you sure you want to delete this table? This action cannot be undone.")),
            new DialogFooter(
                new Button("Cancel", () => { confirmDeleteId.Set((int?)null); }),
                new Button("Delete", async () =>
                {
                  if (confirmDeleteId.Value == null) return;
                  var success = await service.DeleteRevenueEntryAsync(confirmDeleteId.Value.Value);
                  if (success)
                  {
                    refresh.Set(refresh.Value + 1);
                  }
                  confirmDeleteId.Set((int?)null);
                }).Variant(ButtonVariant.Destructive))
        ) : null,
        sheets
    );
  }
}

public class DataTableViewSheet(int entryId, Action onClose) : ViewBase
{
  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var rows = UseState<List<DynamicRow>>([]);
    var columns = UseState<DataTableColumn[]>([]);
    var isLoading = UseState(true);
    var error = UseState("");

    UseEffect(async () =>
    {
      try
      {
        await using var db = factory.CreateDbContext();
        var entry = await db.RevenueEntries.FindAsync(entryId);
        if (entry == null || string.IsNullOrEmpty(entry.JsonData))
        {
          error.Set("Entry not found or empty.");
          isLoading.Set(false);
          return;
        }

        using var doc = JsonDocument.Parse(entry.JsonData);
        var root = doc.RootElement;
        List<Dictionary<string, object?>> dataRows = [];

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
          var first = root[0];
          bool isObj = first.ValueKind == JsonValueKind.Object;
          bool hasFile = isObj && (first.TryGetProperty("FileName", out _) || first.TryGetProperty("fileName", out _));

          if (isObj && hasFile)
          {
            var lastSheet = root.EnumerateArray().LastOrDefault();
            if (lastSheet.ValueKind == JsonValueKind.Object && (lastSheet.TryGetProperty("Rows", out var rowsProp) || lastSheet.TryGetProperty("rows", out rowsProp)))
            {
              dataRows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(rowsProp.GetRawText()) ?? [];
            }
          }
          else
          {
            dataRows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(entry.JsonData) ?? [];
          }
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
          if (root.TryGetProperty("Rows", out var r) || root.TryGetProperty("rows", out r))
            dataRows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(r.GetRawText()) ?? [];
        }

        if (dataRows.Count == 0)
        {
          error.Set("No rows found in this table.");
          isLoading.Set(false);
          return;
        }

        var firstRow = dataRows[0];
        var headers = firstRow.Keys.ToList();

        var cols = headers.Select((h, i) => new DataTableColumn
        {
          Name = $"Col{i}",
          Header = h,
          Order = i,
          Sortable = true,
          Filterable = true,
          ColType = ColType.Text
        }).ToArray();

        var dRows = dataRows.Select(d =>
        {
          var r = new DynamicRow();
          int i = 0;
          foreach (var h in headers)
          {
            r.SetValue(i, d.GetValueOrDefault(h));
            i++;
          }
          return r;
        }).ToList();

        columns.Set(cols);
        rows.Set(dRows);
      }
      catch (Exception ex)
      {
        error.Set($"Error loading data: {ex.Message}");
      }
      finally
      {
        isLoading.Set(false);
      }
    }, []);

    object content;
    if (isLoading.Value)
    {
      content = Layout.Center().Height(Size.Full()).Add(Text.Label("Loading Table..."));
    }
    else if (!string.IsNullOrEmpty(error.Value))
    {
      content = Layout.Center().Height(Size.Full()).Add(Text.Label(error.Value).Color(Colors.Red));
    }
    else
    {
      var config = new DataTableConfig
      {
        FreezeColumns = 1,
        ShowGroups = false,
        ShowIndexColumn = true,
        ShowSearch = true,
        AllowSorting = true,
        AllowFiltering = true,
        ShowVerticalBorders = true
      };
      // Use fit height or fraction logic. If Sheet uses FooterLayout, content often needs to fill remaining space.
      // Wrap in a vertical layout with Fraction(1) to ensure it takes available space.
      content = Layout.Vertical().Height(Size.Fraction(1)).Gap(0).Padding(0)
          .Add(new DataTableView(rows.Value.AsQueryable(), Size.Full(), Size.Full(), columns.Value, config));
    }

    var footer = Layout.Horizontal().Align(Align.Right).Padding(6)
        .Add(new Button("Close", onClose).Variant(ButtonVariant.Primary));

    return new Sheet(
        _ => onClose(),
        new FooterLayout(footer, content),
        "Table Viewer",
        $"Viewing data table."
    ).Width(Size.Full());
  }
}
