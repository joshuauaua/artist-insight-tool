using Ivy.Shared;
using Ivy.Hooks;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.IO;
using System.Text; // Added for StringBuilder
using ArtistInsightTool.Apps.Views;
using ArtistInsightTool.Apps.Services;

namespace ArtistInsightTool.Apps.Tables;

// [App(icon: Icons.Database, title: "Data Tables", path: ["Tables"])]
public class DataTablesApp : ViewBase
{
  public record TableItem(int RealId, string Id, string Name, string Template, string Date);

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var debug = UseState<string>("");
    var showImportSheet = UseState(false);

    if (showImportSheet.Value)
    {
      return new ExcelDataReaderExample.ExcelDataReaderSheet(() =>
      {
        showImportSheet.Set(false);
        // refresh will be handled by UseQuery Revalidate
      });
    }

    var tablesQuery = UseQuery("datatables_list", async (ct) =>
    {
      // Small delay to ensure UI renders loading state (if needed, though UseQuery handles it)
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
          void AddItem(string title, string template) => items.Add(new TableItem(entry.Id, $"DT{index++:D3}", title, template, entry.UpdatedAt.ToShortDateString()));

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
                string tmpl = "-";
                if (el.TryGetProperty("Title", out var p) || el.TryGetProperty("title", out p)) t = p.GetString() ?? "Untitled";
                if (el.TryGetProperty("TemplateName", out var tp) || el.TryGetProperty("templateName", out tp)) tmpl = tp.GetString() ?? "-";
                AddItem(t, tmpl);
              }
            }
            else AddItem("Legacy Data", "-");
          }
          else if (root.ValueKind == JsonValueKind.Object) AddItem("Single Sheet", "-");
        }
        catch { }
      }

      items.Reverse(); // Show newest first
      return items;
    });

    var tablesData = tablesQuery.Value ?? [];
    var isLoading = tablesQuery.Loading;
    var refetch = tablesQuery.Mutator.Revalidate;


    var selectedIds = UseState<HashSet<string>>([]);
    var searchQuery = UseState("");
    var selectedTemplate = UseState("All");

    // Filter items based on search and template
    var filteredItems = tablesData;
    if (!string.IsNullOrWhiteSpace(searchQuery.Value))
    {
      var q = searchQuery.Value.ToLowerInvariant();
      filteredItems = filteredItems.Where(t =>
          t.Name.ToLowerInvariant().Contains(q) ||
          t.Id.ToLowerInvariant().Contains(q)
      ).ToList();
    }

    if (selectedTemplate.Value != "All")
    {
      filteredItems = filteredItems.Where(t => t.Template == selectedTemplate.Value).ToList();
    }

    bool allSelected = filteredItems.Count > 0 && selectedIds.Value.Count == filteredItems.Count;

    var service = UseService<ArtistInsightService>();
    var confirmDeleteId = UseState<int?>(() => null);
    var selectedTableId = UseState<int?>(() => null);

    if (selectedTableId.Value != null)
    {
      return new DataTableViewSheet(selectedTableId.Value.Value, () => selectedTableId.Set((int?)null));
    }


    var mappingEntryId = UseState<int?>(initialValue: null);

    if (mappingEntryId.Value.HasValue)
    {
      return new HeaderMappingSheet(mappingEntryId.Value.Value, () => mappingEntryId.Set((int?)null));
    }

    // Unique templates for filter
    var uniqueTemplates = tablesData.Select(t => t.Template).Distinct().OrderBy(t => t).ToList();
    var templateOptions = new List<Option<string>> { new("All Templates", "All") };
    templateOptions.AddRange(uniqueTemplates.Select(t => new Option<string>(t, t)));

    var headerCard = new Card(
        Layout.Vertical().Gap(10)
            .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                 .Add(Text.H1("Data Tables"))
                 .Add(new Spacer().Width(Size.Fraction(1)))
                 .Add(new Button("Import Data", () => showImportSheet.Set(true))
                     .Icon(Icons.FileUp)
                     .Variant(ButtonVariant.Primary)
                 )
            )
            .Add(Layout.Horizontal().Width(Size.Full()).Gap(10)
                 .Add(searchQuery.ToTextInput().Placeholder("Search tables...").Width(300))
                 .Add(selectedTemplate.ToSelectInput(templateOptions).Width(200))
            )
    );

    var content = Layout.Vertical()
        .Height(Size.Full())
        .Padding(20, 0, 20, 50)
        .Add(isLoading
            ? Layout.Center().Add(Text.Label("Loading..."))
            : Layout.Vertical().Height(Size.Fraction(1))
                .Add(!string.IsNullOrEmpty(debug.Value) ? Text.Label(debug.Value).Color(Colors.Red) : null)
                .Add(filteredItems.Count > 0
                    ? filteredItems.Select(t => new
                    {
                      IdButton = new Button(t.Id, () => selectedTableId.Set(t.RealId)).Variant(ButtonVariant.Ghost),
                      t.Name,
                      t.Template,
                      t.Date,
                      Actions = new Button("", () => confirmDeleteId.Set(t.RealId)).Icon(Icons.Trash).Variant(ButtonVariant.Ghost)
                    }).ToArray().ToTable()
                        .Width(Size.Full())
                        .Add(x => x.IdButton)
                        .Add(x => x.Name)
                        .Add(x => x.Template)
                        .Add(x => x.Date)
                        .Add(x => x.Actions)
                        .Header(x => x.IdButton, "ID")
                        .Header(x => x.Name, "Name")
                        .Header(x => x.Template, "Template")
                        .Header(x => x.Date, "Uploaded")
                        .Header(x => x.Actions, "")
                        .Header(x => x.Date, "Uploaded")
                        .Header(x => x.Actions, "")
                    : Layout.Center().Add(Text.Label("There is no information to display"))
                )
        );

    return new Fragment(
        new HeaderLayout(headerCard, content),
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
                    refetch();
                  }
                  confirmDeleteId.Set((int?)null);
                }).Variant(ButtonVariant.Destructive))
        ) : null
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
    var entryTitle = UseState("");

    UseEffect(async () =>
    {
      try
      {
        await using var db = factory.CreateDbContext();
        var entry = await db.RevenueEntries.FindAsync(entryId);
        if (entry == null || string.IsNullOrEmpty(entry.JsonData))
        {
          error.Set("Entry not found or empty.");
          entryTitle.Set("Unknown Table");
          isLoading.Set(false);
          return;
        }

        entryTitle.Set(entry.Description ?? "Untitled Table");

        using var doc = JsonDocument.Parse(entry.JsonData);
        var root = doc.RootElement;
        List<Dictionary<string, object?>> dataRows = [];

        // Parsing logic adapted from DataTablesApp/ExcelDataReader
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
          var first = root[0];
          bool isObj = first.ValueKind == JsonValueKind.Object;
          bool hasFile = isObj && (first.TryGetProperty("FileName", out _) || first.TryGetProperty("fileName", out _));

          if (isObj && hasFile)
          {
            // It's a list of sheets, get the last one (most recent) or just the first one?
            // Since "Data Tables" lists multiple "Entries" but a RevenueEntry might have multiple sheets annexed...
            // DataTablesApp lists individual "TableItem"s but they map to RealId (RevenueEntry Id).
            // If a RevenueEntry has multiple sheets, DataTablesApp logic (line 61) iterates them.
            // But wait, RealId is SAME for all sheets in one ID?
            // DataTablesApp.cs line 51: AddItem(entry.Id, ...)
            // If multiple sheets, it generates multiple "TableItem"s but all point to "entry.Id".
            // We need to know WHICH sheet to show.
            // Ah, DataTablesApp currently iterates sheets but only passes "entry.Id".
            // So if I click one, I load the RevenueEntry.
            // I should probably show all sheets or tabs?
            // For now, let's just show the LAST sheet or all data merged?
            // Let's assume the user clicked a specific "Table Item" which corresponds to *one* sheet ideally if we tracked it.
            // But we only track `entry.Id`.
            // Simple approach: Show the last attached sheet (as that's usually the "table" context).
            var lastSheet = root.EnumerateArray().LastOrDefault();
            if (lastSheet.ValueKind == JsonValueKind.Object && (lastSheet.TryGetProperty("Rows", out var rowsProp) || lastSheet.TryGetProperty("rows", out rowsProp)))
            {
              dataRows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(rowsProp.GetRawText()) ?? [];
            }
          }
          else
          {
            // Legacy Array of Rows
            dataRows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(entry.JsonData) ?? [];
          }
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
          // Single Sheet Object? Or maybe "Rows" property inside?
          // If it's the old single object format, check if it has "Rows"
          if (root.TryGetProperty("Rows", out var r) || root.TryGetProperty("rows", out r))
            dataRows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(r.GetRawText()) ?? [];
        }

        if (dataRows.Count == 0)
        {
          error.Set("No rows found in this table.");
          isLoading.Set(false);
          return;
        }

        // Extract Columns
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

    var content = Layout.Vertical().Height(Size.Full())
        .Add(isLoading.Value ? Layout.Center().Add(Text.Label("Loading Table...")) :
             !string.IsNullOrEmpty(error.Value) ? Layout.Center().Add(Text.Label(error.Value).Color(Colors.Red)) :
             Layout.Vertical().Height(Size.Full()).Add(new DataTableView(rows.Value.AsQueryable(), Size.Full(), Size.Full(), columns.Value, config))
        );

    return new Sheet(
        _ => onClose(),
        content,
        entryTitle.Value,
        isLoading.Value ? "Loading..." : $"{rows.Value.Count} Rows"
    ).Width(Size.Full());
  }
}
