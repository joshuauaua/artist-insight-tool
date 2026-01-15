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
          .Include(e => e.Track).ThenInclude(t => t.Album)
          .Include(e => e.Artist)
          .Include(e => e.Source)
          .Include(e => e.Album)
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

        return Layout.Vertical().Gap(10).Width(Size.Full())
           .Add(Layout.Horizontal().Align(Align.Center).Gap(10)
            .Add(new Button("Back to Details", () => viewingSheetIndex.Set((int?)null)).Variant(ButtonVariant.Link).Icon(Icons.ArrowLeft))
               .Add(Text.H4(currentSheet.Title ?? "Annexed Data"))
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
    var name = e.Track?.Title ?? e.Album?.Title ?? "-";
    var type = e.Track != null ? "Track" : (e.Album != null ? "Album" : "Other");

    // --- RENDER Edit Form ---
    // infoName was unused in duplication, reusing original logic if helpful or just removing dupes.
    var infoName = e.Track?.Title ?? e.Album?.Title ?? "-";

    return Layout.Vertical().Gap(15)
        // 1. Name (Description)
        .Add(Layout.Vertical().Gap(5)
            .Add(Text.Label("Name"))
            .Add(descriptionState.ToTextInput().Placeholder("Enter name..."))
        )
        // 2. Date
        .Add(Layout.Vertical().Gap(5)
            .Add(Text.Label("Date (MM/dd/yyyy)"))
            .Add(dateStringState.ToTextInput())
        )
        // 3. Amount
        .Add(Layout.Vertical().Gap(5)
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
                .Add(Layout.Vertical().Gap(5)
                     .Add(sheets.Select((s, i) =>
                          Layout.Vertical().Gap(2)
                              .Add(new Button(string.IsNullOrEmpty(s.Title) ? s.FileName : s.Title, () => viewingSheetIndex.Set(i))
                                 .Variant(ButtonVariant.Outline)
                                 .Icon(Icons.Sheet)
                                 .Width(Size.Full())
                              )
                              .Add(Text.Muted($"{s.FileName} • {s.TemplateName}"))
                     ).ToArray())
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

public class DynamicRow
{
  private readonly Dictionary<int, object?> _values = new();
  public void SetValue(int index, object? value) => _values[index] = value;
  public object? GetValue(int index) => _values.TryGetValue(index, out var v) ? v : null;

  public object? Col0 => GetValue(0); public object? Col1 => GetValue(1); public object? Col2 => GetValue(2);
  public object? Col3 => GetValue(3); public object? Col4 => GetValue(4); public object? Col5 => GetValue(5);
  public object? Col6 => GetValue(6); public object? Col7 => GetValue(7); public object? Col8 => GetValue(8);
  public object? Col9 => GetValue(9); public object? Col10 => GetValue(10);
  public object? Col11 => GetValue(11); public object? Col12 => GetValue(12); public object? Col13 => GetValue(13);
  public object? Col14 => GetValue(14); public object? Col15 => GetValue(15); public object? Col16 => GetValue(16);
  public object? Col17 => GetValue(17); public object? Col18 => GetValue(18); public object? Col19 => GetValue(19);
  public object? Col20 => GetValue(20); public object? Col21 => GetValue(21); public object? Col22 => GetValue(22);
  public object? Col23 => GetValue(23); public object? Col24 => GetValue(24); public object? Col25 => GetValue(25);
  public object? Col26 => GetValue(26); public object? Col27 => GetValue(27); public object? Col28 => GetValue(28);
  public object? Col29 => GetValue(29); public object? Col30 => GetValue(30); public object? Col31 => GetValue(31);
  public object? Col32 => GetValue(32); public object? Col33 => GetValue(33); public object? Col34 => GetValue(34);
  public object? Col35 => GetValue(35); public object? Col36 => GetValue(36); public object? Col37 => GetValue(37);
  public object? Col38 => GetValue(38); public object? Col39 => GetValue(39); public object? Col40 => GetValue(40);
  public object? Col41 => GetValue(41); public object? Col42 => GetValue(42); public object? Col43 => GetValue(43);
  public object? Col44 => GetValue(44); public object? Col45 => GetValue(45); public object? Col46 => GetValue(46);
  public object? Col47 => GetValue(47); public object? Col48 => GetValue(48); public object? Col49 => GetValue(49);
}

public class AnnexSheetData
{
  public string? Title { get; set; }
  public string? FileName { get; set; }
  public string? TemplateName { get; set; }
  public List<Dictionary<string, object?>>? Rows { get; set; }
}
