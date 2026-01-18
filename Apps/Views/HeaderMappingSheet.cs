using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ArtistInsightTool.Apps.Views;

public class HeaderMappingSheet(int entryId, Action onClose) : ViewBase
{
  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var headers = UseState<List<string>>([]);
    var mappings = UseState<Dictionary<string, string>>([]);
    var isLoading = UseState(true);

    UseEffect(async () =>
    {
      await using var db = factory.CreateDbContext();
      var entry = await db.RevenueEntries.FindAsync(entryId);
      if (entry != null && !string.IsNullOrWhiteSpace(entry.JsonData))
      {
        try
        {
          using var doc = JsonDocument.Parse(entry.JsonData);
          if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
          {
            var first = doc.RootElement[0];
            var props = new List<string>();
            // Handle simple array of objects
            if (first.ValueKind == JsonValueKind.Object)
            {
              foreach (var prop in first.EnumerateObject())
              {
                props.Add(prop.Name);
              }

              // Handle specific structure where real data might be nested? 
              // Previous code in DataTablesApp checked for "FileName" etc.
              // Assuming standard table structure for now based on "headers of a datatable"
            }
            headers.Set(props);
          }
        }
        catch { }

        if (!string.IsNullOrEmpty(entry.ColumnMapping))
        {
          try
          {
            var existing = JsonSerializer.Deserialize<Dictionary<string, string>>(entry.ColumnMapping);
            if (existing != null) mappings.Set(existing);
          }
          catch { }
        }
      }
      isLoading.Set(false);
    }, []);

    var fieldOptions = new[]
    {
        "Assets", "Territory", "Label", "Collection", "Artist", "Store", "Gross", "Net", "Ignore"
    }.Select(f => new Option<string>(f, f));

    return new Dialog(
        _ => onClose(),
        new DialogHeader("Map Headers"),
        new DialogBody(
            isLoading.Value
            ? Layout.Center().Add(Text.Label("Loading..."))
            : Layout.Vertical().Gap(10)
                .Add(headers.Value.Select(header =>
                {
                  var currentVal = mappings.Value.TryGetValue(header, out var v) ? v : "Select Field";
                  var menu = new DropDownMenu(
                      DropDownMenu.DefaultSelectHandler(),
                      new Button(currentVal, () => { }).Variant(ButtonVariant.Outline).Width(150)
                  );

                  foreach (var opt in fieldOptions)
                  {
                    menu = menu | MenuItem.Default(opt.Label).HandleSelect(() =>
                    {
                      var newMap = new Dictionary<string, string>(mappings.Value);
                      newMap[header] = (string)opt.Value;
                      mappings.Set(newMap);
                    });
                  }

                  return Layout.Horizontal().Align(Align.Center).Gap(10)
                      .Add(Text.Label(header).Width(150))
                      .Add(menu);
                }))
                .Add(Layout.Horizontal().Align(Align.Right).Gap(10).Padding(20, 0, 0, 0)
                    .Add(new Button("Cancel", onClose))
                    .Add(new Button("Save", async () =>
                    {
                      await using var db = factory.CreateDbContext();
                      var entry = await db.RevenueEntries.FindAsync(entryId);
                      if (entry != null)
                      {
                        entry.ColumnMapping = JsonSerializer.Serialize(mappings.Value);
                        await db.SaveChangesAsync();
                      }
                      onClose();
                    }).Variant(ButtonVariant.Primary))
                )
        ),
        new DialogFooter()
    );
  }
}
