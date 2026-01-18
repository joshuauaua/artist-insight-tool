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
          string? templateName = null;
          using var doc = JsonDocument.Parse(entry.JsonData);

          if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
          {
            var first = doc.RootElement[0];
            if (first.ValueKind == JsonValueKind.Object)
            {
              if (first.TryGetProperty("TemplateName", out var tNameProp))
              {
                templateName = tNameProp.GetString();
              }
            }
          }

          if (!string.IsNullOrEmpty(templateName))
          {
            var template = await db.ImportTemplates.FirstOrDefaultAsync(t => t.Name == templateName);
            if (template != null)
            {
              headers.Set(template.GetHeaders());
            }
            else
            {
              // Fallback to extraction if template not found
              ExtractHeadersFromData(doc, headers);
            }
          }
          else
          {
            // Fallback if no template name
            ExtractHeadersFromData(doc, headers);
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

    void ExtractHeadersFromData(JsonDocument doc, IState<List<string>> headersState)
    {
      if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
      {
        var first = doc.RootElement[0];
        var props = new List<string>();

        if (first.ValueKind == JsonValueKind.Object)
        {
          if (first.TryGetProperty("Rows", out var rowsProp) && rowsProp.ValueKind == JsonValueKind.Array && rowsProp.GetArrayLength() > 0)
          {
            var firstRow = rowsProp[0];
            if (firstRow.ValueKind == JsonValueKind.Object)
            {
              foreach (var prop in firstRow.EnumerateObject()) { props.Add(prop.Name); }
            }
          }
          else
          {
            foreach (var prop in first.EnumerateObject()) { props.Add(prop.Name); }
          }
        }
        headersState.Set(props);
      }
    }

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

                  // Keep button width constrained
                  var menuBtn = new Button(currentVal, () => { }).Variant(ButtonVariant.Outline).Width(200);

                  var menu = new DropDownMenu(
                      DropDownMenu.DefaultSelectHandler(),
                      menuBtn
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

                  // Assign Fraction(1) to the Label so it takes all available space
                  return Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                      .Add(Text.Label(header ?? "Unknown").Width(Size.Fraction(1)))
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
