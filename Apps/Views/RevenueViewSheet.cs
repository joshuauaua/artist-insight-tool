using System.Globalization;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Ivy.Shared;

namespace ArtistInsightTool.Apps.Views;

public class RevenueViewSheet(int id, Action onClose, Action onEdit) : ViewBase
{
  private readonly int _id = id;
  private readonly Action _onClose = onClose;
  private readonly Action _onEdit = onEdit;

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    // Data Viewing State
    var viewingSheetIndex = UseState<int?>(() => null);

    // Entry State
    var entryState = UseState<RevenueEntry?>(() => null);

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
      }
      return null;
    }

    UseEffect(LoadEntry, [EffectTrigger.AfterInit()]);

    if (entryState.Value is null) return Layout.Vertical().Gap(10).Add(Text.Muted("Loading..."));

    var e = entryState.Value;
    var hasAnnexData = !string.IsNullOrEmpty(e.JsonData);

    // Parse Metadata & Data (Reuse logic from RevenueEditSheet)
    var sheets = new List<AnnexSheetData>();

    if (hasAnnexData)
    {
      try
      {
        using var doc = System.Text.Json.JsonDocument.Parse(e.JsonData!);
        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
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
            var rows = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(e.JsonData!);
            if (rows != null) sheets.Add(new AnnexSheetData { Title = "Legacy Data", FileName = "Legacy", TemplateName = "Unknown", Rows = rows });
          }
        }
        else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
          var sheet = System.Text.Json.JsonSerializer.Deserialize<AnnexSheetData>(e.JsonData!);
          if (sheet != null) sheets.Add(sheet);
        }
      }
      catch { }
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
        return Layout.Vertical().Gap(10)
            .Add(new Button("Back", () => viewingSheetIndex.Set((int?)null)).Variant(ButtonVariant.Link))
            .Add(Text.Muted("No valid data found in this sheet."));
      }
    }

    var infoName = e.Track?.Title ?? e.Album?.Title ?? "-";
    var type = e.Track != null ? "Track" : (e.Album != null ? "Album" : "Other");

    return Layout.Vertical().Gap(15)
        // 1. Name & Description
        .Add(Layout.Vertical().Gap(5)
            .Add(Text.H4(e.Description ?? "-"))
        )
        // 2. Date & Amount Row
        .Add(Layout.Horizontal().Gap(20)
            .Add(Layout.Vertical().Gap(5).Width(Size.Fraction(1))
                .Add(Text.Label("Date"))
                .Add(Text.P(e.RevenueDate.ToShortDateString()))
            )
            .Add(Layout.Vertical().Gap(5).Width(Size.Fraction(1))
                .Add(Text.Label("Amount"))
                .Add(Text.H4(e.Amount.ToString("C", CultureInfo.GetCultureInfo("sv-SE"))))
            )
        )
        // 3. Category & Source Info Card
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
        // 4. Annexed Data
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
        .Add(Layout.Horizontal().Align(Align.Right).Gap(10).Padding(10, 0, 0, 0)
             .Add(new Button("Close", _onClose))
             .Add(new Button("Edit", _onEdit).Variant(ButtonVariant.Primary).Icon(Icons.Pencil))
        );
  }
}
