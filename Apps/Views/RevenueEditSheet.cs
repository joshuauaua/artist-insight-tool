using System.Globalization;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Ivy.Shared;

namespace ArtistInsightTool.Apps.Views;

public class RevenueEditSheet(int id, Action onClose) : ViewBase
{
  private readonly int _id = id;
  private readonly Action _onClose = onClose;

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    // Data Viewing State
    var viewingSheetIndex = UseState<int?>(() => null);

    // Form States (Restored)
    var entryState = UseState<RevenueEntry?>(() => null);
    var amountState = UseState("");
    var descriptionState = UseState("");
    var dateState = UseState(DateTime.Now);
    var dateStringState = UseState("");



    async Task<IDisposable?> LoadEntry()
    {
      await using var db = factory.CreateDbContext();
      var data = await db.RevenueEntries
          .Include(e => e.Artist)
          .Include(e => e.Source)
          .FirstOrDefaultAsync(e => e.Id == _id);

      if (data != null)
      {
        entryState.Set(data);
        amountState.Set(data.Amount.ToString(CultureInfo.InvariantCulture));
        descriptionState.Set(data.Description ?? "");
        dateState.Set(data.RevenueDate);
        dateStringState.Set(data.RevenueDate.ToString("MM/dd/yyyy"));
      }
      return null;
    }

    UseEffect(LoadEntry, [EffectTrigger.AfterInit()]);

    if (entryState.Value is null) return Layout.Vertical().Gap(10).Add(Text.Muted("Loading..."));

    var e = entryState.Value;
    var hasAnnexData = !string.IsNullOrEmpty(e.JsonData);

    // Parse Metadata & Data
    var sheets = new List<AnnexSheetData>();

    if (hasAnnexData)
    {
      try
      {
        using var doc = System.Text.Json.JsonDocument.Parse(e.JsonData!);
        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
          // Check if already list of sheets (properties check)
          var isListOfSheets = false;
          if (doc.RootElement.GetArrayLength() > 0 && doc.RootElement[0].ValueKind == System.Text.Json.JsonValueKind.Object)
          {
            if (doc.RootElement[0].TryGetProperty("FileName", out _)) isListOfSheets = true;
          }

          if (isListOfSheets)
          {
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<AnnexSheetData>>(e.JsonData!);
            if (deserialized != null) sheets.AddRange(deserialized);
          }
          else
          {
            // Legacy Rows
            var rows = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(e.JsonData!);
            if (rows != null) sheets.Add(new AnnexSheetData { Title = "Legacy Data", FileName = "Legacy", TemplateName = "Unknown", Rows = rows });
          }
        }
        else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
          // Single Sheet
          var sheet = System.Text.Json.JsonSerializer.Deserialize<AnnexSheetData>(e.JsonData!);
          if (sheet != null) sheets.Add(sheet);
        }
      }
      catch { /* Ignore parse errors */ }
    }


    // --- RENDER annex data view ---
    if (viewingSheetIndex.Value.HasValue && viewingSheetIndex.Value < sheets.Count)
    {
      var currentSheet = sheets[viewingSheetIndex.Value.Value];
      var parsedRows = currentSheet.Rows;

      if (parsedRows != null && parsedRows.Count > 0)
      {
        var headers = parsedRows[0].Keys.Take(50).ToArray();
        var dRows = parsedRows.Select(d =>
        {
          var r = new DynamicRow();
          int i = 0;
          foreach (var h in headers) { r.SetValue(i, d.GetValueOrDefault(h)); i++; }
          return r;
        }).AsQueryable();

        var cols = headers.Select((h, i) => new DataTableColumn
        {
          Name = $"Col{i}",
          Header = h,
          Order = i,
          Sortable = true,
          Filterable = true,
          ColType = ColType.Text
        }).Cast<DataTableColumn>().ToArray();

        return Layout.Vertical().Gap(5).Width(Size.Full())
           .Add(Layout.Horizontal().Align(Align.Center).Gap(10)
               .Add(new Button("Back", () => viewingSheetIndex.Set((int?)null)).Variant(ButtonVariant.Primary).Icon(Icons.ArrowLeft))
               .Add(Text.H4(currentSheet.Title ?? "Annexed Data"))
               .Add(new Spacer())
               .Add(Text.Muted($"{currentSheet.FileName}"))
           )
           .Add(new DataTableView(dRows, Size.Full(), Size.Fit(), cols, new DataTableConfig { ShowSearch = true, AllowSorting = true }));
      }
      else
      {
        // Empty
        return Layout.Vertical().Gap(10)
            .Add(new Button("Back", () => viewingSheetIndex.Set((int?)null)).Variant(ButtonVariant.Link))
            .Add(Text.Muted("No valid data found in this sheet."));
      }
    }

    // --- RENDER Edit Form ---
    var name = "-";
    var type = "Other";

    // --- RENDER Edit Form ---
    // infoName was unused in duplication, reusing original logic if helpful or just removing dupes.
    var infoName = "-";

    return Layout.Vertical().Gap(10)
        // 1. Name (Description)
        .Add(Layout.Vertical().Gap(2)
            .Add(Text.Label("Name"))
            .Add(descriptionState.ToTextInput().Placeholder("Enter name..."))
        )
        // 2. Date
        .Add(Layout.Vertical().Gap(2)
            .Add(Text.Label("Date (MM/dd/yyyy)"))
            .Add(dateStringState.ToTextInput())
        )
        // 3. Amount
        .Add(Layout.Vertical().Gap(2)
            .Add(Text.Label("Amount ($)"))
            .Add(amountState.ToTextInput().Placeholder("0.00"))
        )
        // 4. Category & Source Info
        .Add(new Card(
             Layout.Vertical().Gap(5)
                 .Add(Layout.Horizontal().Gap(10).Align(Align.Center)
                     .Add(Layout.Vertical().Gap(2)
                         .Add(Text.Label("Source"))
                         .Add(Text.Muted(e.Source.DescriptionText))
                     )
                     .Add(new Spacer())
                     .Add(Layout.Vertical().Gap(2).Align(Align.Center)
                         .Add(Text.Label("Category"))
                         .Add(Text.Muted(type))
                     )
                 )
                 .Add(Text.Muted($"Linked to: {infoName}"))
        ))
        // 5. Annexed Data
        .Add(sheets.Count > 0
            ? Layout.Vertical().Gap(5)
                .Add(Text.Label("Annexed Data"))
                .Add(
                    viewingSheetIndex.ToSelectInput(
                        sheets.Select((s, i) => new Option<int?>(
                            $"{(!string.IsNullOrEmpty(s.Title) ? s.Title : s.FileName)} | {s.TemplateName ?? "-"}",
                            i
                        )).ToList()
                    ).Placeholder("Select annexed file to view...")
                )
            : null
        )
        // Actions
        .Add(Layout.Horizontal().Align(Align.Center).Gap(10)
             .Add(new Button("Delete", async () =>
             {
               await using var db = factory.CreateDbContext();
               var entry = await db.RevenueEntries.FindAsync(_id);
               if (entry != null)
               {
                 db.RevenueEntries.Remove(entry);
                 await db.SaveChangesAsync();
               }
               _onClose();
             }).Variant(ButtonVariant.Destructive))
             .Add(new Spacer())
             .Add(new Button("Save Changes", async () =>
             {
               if (decimal.TryParse(amountState.Value, out var newAmount))
               {
                 await using var db = factory.CreateDbContext();
                 var entry = await db.RevenueEntries.FindAsync(_id);
                 if (entry != null)
                 {
                   entry.Amount = newAmount;
                   entry.Description = descriptionState.Value;
                   if (DateTime.TryParse(dateStringState.Value, out var newDate))
                   {
                     entry.RevenueDate = newDate;
                   }
                   await db.SaveChangesAsync();
                 }
                 _onClose();
               }
             }).Variant(ButtonVariant.Primary))
        );
  }
}



public class AnnexSheetData
{
  public string? Title { get; set; }
  public string? FileName { get; set; }
  public string? TemplateName { get; set; }
  public List<Dictionary<string, object?>>? Rows { get; set; }
}
