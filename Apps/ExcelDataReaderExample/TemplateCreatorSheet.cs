using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static ExcelDataReaderExample.ExcelDataReaderSheet;

namespace ExcelDataReaderExample;

public class TemplateCreatorSheet(CurrentFile? file, Action onSuccess, Action onCancel) : ViewBase
{
  private readonly CurrentFile? _file = file;
  private readonly Action _onSuccess = onSuccess;
  private readonly Action _onCancel = onCancel;

  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();

    // --- Template Creation State ---
    var newTemplateName = UseState("");
    var newTemplateCategory = UseState("Merchandise");
    var templateCreationStep = UseState(1);

    // --- Mapping State ---
    var selectedHeaderToMap = UseState<string?>(() => null);
    var selectedFieldToMap = UseState<string?>(() => null);
    var mappedPairs = UseState<List<(string Header, string FieldKey)>>(() => new());

    var analysis = _file?.Analysis;
    if (analysis?.Sheets.Count == 0) return Text.Muted("No sheets found in file.");
    var headers = analysis!.Sheets[0].Headers;

    var fieldGroups = new Dictionary<string, List<(string Key, string Label)>>
        {
            { "Global Columns", new()
                {
                    ("TransactionDate", "Transaction Date"),
                    ("TransactionId", "Transaction ID"),
                    ("SourcePlatform", "Source Platform"),
                    ("Category", "Category"),
                    ("Quantity", "Quantity"),
                    ("Territory", "Territory/Region")
                }
            },
            { "Financials", new()
                {
                    ("Gross", "Gross Revenue"),
                    ("Net", "Net Revenue"),
                    ("Amount", "Amount (Net)"),
                    ("Currency", "Currency")
                }
            },
            { "Assets", new()
                {
                    ("Asset", "Asset Name (Item)"),
                    ("Collection", "Asset Group (Parent)"),
                    ("Artist", "Artist"),
                    ("Label", "Label")
                }
            }
        };

    var categorySpecificGroups = new Dictionary<string, Dictionary<string, List<(string Key, string Label)>>>
        {
            { "Merchandise", new() {
                { "Retail", new() { ("Sku", "SKU"), ("Store", "Store") } },
                { "Customer", new() { ("CustomerEmail", "Customer Email") } }
            }},
            { "Royalties", new() {
                { "Music Identifiers", new() { ("Isrc", "ISRC"), ("Upc", "UPC"), ("Dsp", "DSP") } }
            }},
            { "Concerts", new() {
                { "Event Details", new() { ("VenueName", "Venue Name"), ("EventStatus", "Event Status"), ("TicketClass", "Ticket Class") } }
            }}
        };

    Func<string, string?> getHeader = k => mappedPairs.Value.FirstOrDefault(m => m.FieldKey == k).Header;

    var content = Layout.Vertical().Gap(10).Width(Size.Full());

    if (templateCreationStep.Value == 1)
    {
      content
          .Add(Text.H4("Step 1: Template Details"))
          .Add(Layout.Vertical().Gap(2)
              .Add(Text.Label("Template Name"))
              .Add(newTemplateName.ToTextInput().Placeholder("e.g. Spotify Report")))
          .Add(Layout.Vertical().Gap(2)
              .Add(Text.Label("Category"))
              .Add(newTemplateCategory.ToSelectInput(new[] { "Merchandise", "Royalties", "Concerts", "Other" }.Select(c => new Option<string>(c, c)))))
          .Add(Layout.Horizontal().Align(Align.Right).Padding(10, 0, 0, 0)
              .Add(new Button("Next", () =>
              {
                if (string.IsNullOrWhiteSpace(newTemplateName.Value)) { client.Toast("Name required", "Warning"); return; }
                templateCreationStep.Set(2);
              }).Variant(ButtonVariant.Primary).Icon(Icons.ArrowRight)));
    }
    else
    {
      // Step 2: Mapping
      var activeGroups = new Dictionary<string, List<(string Key, string Label)>>(fieldGroups);

      if (categorySpecificGroups.TryGetValue(newTemplateCategory.Value, out var extraGroups))
      {
        foreach (var grp in extraGroups)
        {
          activeGroups[grp.Key] = grp.Value;
        }
      }

      var mapped = mappedPairs.Value;
      var unmappedHeaders = headers.Where(h => !mapped.Any(m => m.Header == h)).ToList();

      // Flatten groups into SelectInput options with Separators
      var fieldOptions = new List<Option<string?>>();
      var allFieldsMap = new Dictionary<string, string>(); // For lookup later

      foreach (var group in activeGroups)
      {
        // Separator (assuming SelectInput handles or just renders as is)
        fieldOptions.Add(new Option<string?>(null, null) { Label = $"--- {group.Key} ---" }); // Removed Disabled property usage if invalid

        foreach (var field in group.Value)
        {
          allFieldsMap[field.Key] = field.Label;
          // Only add if not already mapped
          if (!mapped.Any(m => m.FieldKey == field.Key))
          {
            fieldOptions.Add(new Option<string?>(field.Label, field.Key));
          }
        }
      }

      content
          .Add(Layout.Horizontal().Align(Align.Center).Add(Text.H4("Step 2: Map Columns")))
          .Add(Layout.Grid().Columns(2).Gap(20)
              .Add(new Card(
                  Layout.Vertical().Gap(5)
                      .Add(Text.H5("1. Select File Header"))
                      .Add(selectedHeaderToMap.ToSelectInput(unmappedHeaders.Select(h => new Option<string?>(h, h)))
                          .Placeholder("Choose header..."))
              ))
              .Add(new Card(
                  Layout.Vertical().Gap(5)
                      .Add(Text.H5("2. Select System Field"))
                      .Add(selectedFieldToMap.ToSelectInput(fieldOptions)
                          .Placeholder("Choose field...")
                          .Variant(SelectInputs.List)
                          .Height(300))
              ))
          )
          .Add(Layout.Horizontal().Align(Align.Center).Padding(10)
              .Add(new Button("Confirm Mapping", () =>
              {
                if (selectedHeaderToMap.Value != null && selectedFieldToMap.Value != null)
                {
                  var list = mappedPairs.Value.ToList();
                  list.Add((selectedHeaderToMap.Value, selectedFieldToMap.Value));
                  mappedPairs.Set(_ => list);
                  selectedHeaderToMap.Set((string?)null);
                  selectedFieldToMap.Set((string?)null);
                }
              }).Variant(ButtonVariant.Primary)
                .Disabled(selectedHeaderToMap.Value == null || selectedFieldToMap.Value == null)
                .Icon(Icons.Link))
          )
          .Add(Text.H5("Mapped Columns"))
          .Add(Layout.Vertical().Padding(15).Gap(5)
              .Add(mapped.Count == 0 ?
                  Layout.Horizontal().Align(Align.Center).Add(Text.Muted("No columns mapped yet."))
                  : Layout.Vertical().Gap(5).Add(mapped.Select(m =>
                      Layout.Horizontal().Gap(10).Align(Align.Center).Padding(5)
                          .Add(new Icon(Icons.Check).Size(16))
                          .Add(Text.Label($"{m.Header}"))
                          .Add(new Icon(Icons.ArrowRight).Size(14))
                          .Add(Text.Label($"{allFieldsMap.GetValueOrDefault(m.FieldKey, m.FieldKey)}"))
                          .Add(Layout.Horizontal().Grow())
                          .Add(new Button("", () =>
                          {
                            var list = mappedPairs.Value.ToList();
                            list.RemoveAll(x => x.Header == m.Header && x.FieldKey == m.FieldKey);
                            mappedPairs.Set(_ => list);
                          }).Variant(ButtonVariant.Ghost).Icon(Icons.Trash).Size(24))
                  ).ToArray())
              )
          )
          .Add(Layout.Horizontal().Gap(10).Align(Align.Right).Padding(10, 0, 0, 0)
              .Add(new Button("Back", () => templateCreationStep.Set(1)).Variant(ButtonVariant.Ghost).Icon(Icons.ArrowLeft))
              .Add(new Button("Save Template", async () =>
              {
                await using var db = factory.CreateDbContext();
                var newT = new ImportTemplate
                {
                  Name = newTemplateName.Value,
                  Category = newTemplateCategory.Value,
                  HeadersJson = JsonSerializer.Serialize(headers),
                  AssetColumn = getHeader("Asset"),
                  AmountColumn = getHeader("Amount"),
                  CollectionColumn = getHeader("Collection"),
                  GrossColumn = getHeader("Gross"),
                  CurrencyColumn = getHeader("Currency"),
                  TerritoryColumn = getHeader("Territory"),
                  LabelColumn = getHeader("Label"),
                  ArtistColumn = getHeader("Artist"),
                  StoreColumn = getHeader("Store"),
                  DspColumn = getHeader("Dsp"),
                  NetColumn = getHeader("Net") ?? getHeader("Amount"),
                  TransactionDateColumn = getHeader("TransactionDate"),
                  TransactionIdColumn = getHeader("TransactionId"),
                  SourcePlatformColumn = getHeader("SourcePlatform"),
                  CategoryColumn = getHeader("Category"),
                  QuantityColumn = getHeader("Quantity"),
                  SkuColumn = getHeader("Sku"),
                  CustomerEmailColumn = getHeader("CustomerEmail"),
                  IsrcColumn = getHeader("Isrc"),
                  UpcColumn = getHeader("Upc"),
                  VenueNameColumn = getHeader("VenueName"),
                  EventStatusColumn = getHeader("EventStatus"),
                  TicketClassColumn = getHeader("TicketClass"),
                  CreatedAt = DateTime.UtcNow,
                  UpdatedAt = DateTime.UtcNow
                };
                db.ImportTemplates.Add(newT);
                await db.SaveChangesAsync();

                var qs = UseService<IQueryService>();
                qs.RevalidateByTag("ImportTemplates");

                client.Toast("Template Created", "Success");
                _onSuccess();
              }).Variant(ButtonVariant.Primary).Disabled(mapped.Count == 0))
          );
    }

    return new Sheet(
        _ => { _onCancel(); return ValueTask.CompletedTask; },
        content,
        "Create Import Template",
        "Define how to import this file format."
    ).Width(Size.Full());
  }
}
