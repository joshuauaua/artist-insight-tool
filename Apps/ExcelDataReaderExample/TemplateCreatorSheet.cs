using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using static ExcelDataReaderExample.ExcelDataReaderSheet;

namespace ExcelDataReaderExample;

public class TemplateCreatorSheet(CurrentFile? file, Action onSuccess, Action onCancel) : ViewBase
{
  private readonly CurrentFile? _file = file;
  private readonly Action _onSuccess = onSuccess;
  private readonly Action _onCancel = onCancel;

  public enum SystemField
  {
    [Display(Name = "Transaction Date")] TransactionDate,
    [Display(Name = "Transaction ID")] TransactionId,
    [Display(Name = "Source Platform")] SourcePlatform,
    [Display(Name = "Category")] Category,
    [Display(Name = "Asset Type (Collection Type)")] AssetType,
    [Display(Name = "Territory/Region")] Territory,
    [Display(Name = "Gross Revenue")] Gross,
    [Display(Name = "Net Revenue")] Net,
    [Display(Name = "Amount (Net)")] Amount,
    [Display(Name = "Currency")] Currency,
    [Display(Name = "Asset Name (Item)")] Asset,
    [Display(Name = "Asset Group (Parent)")] Collection,
    [Display(Name = "Artist")] Artist,
    [Display(Name = "Label")] Label,
    [Display(Name = "Quantity")] Quantity,
    [Display(Name = "SKU")] Sku,
    [Display(Name = "Store")] Store,
    [Display(Name = "Customer Email")] CustomerEmail,
    [Display(Name = "ISRC")] Isrc,
    [Display(Name = "UPC")] Upc,
    [Display(Name = "DSP")] Dsp,
    [Display(Name = "Venue Name")] VenueName,
    [Display(Name = "Event Status")] EventStatus,
    [Display(Name = "Ticket Class")] TicketClass
  }

  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();
    var queryService = UseService<IQueryService>();

    // --- Template Creation State ---
    var newTemplateName = UseState("");
    var newTemplateSourceName = UseState("");
    var newTemplateCategory = UseState("Merchandise");
    var templateCreationStep = UseState(1);

    // --- Mapping State ---
    var selectedHeaderToMap = UseState<string?>(() => null);
    var selectedFieldToMap = UseState<SystemField?>(() => null);
    var mappedPairs = UseState<List<(string Header, string FieldKey)>>(() => new());

    var analysis = _file?.Analysis;
    if (analysis?.Sheets.Count == 0) return Text.Muted("No sheets found in file.");
    var headers = analysis!.Sheets[0].Headers;

    var fieldGroups = new Dictionary<string, List<SystemField>>
        {
            { "Global", new() { SystemField.TransactionDate, SystemField.TransactionId, SystemField.SourcePlatform, SystemField.Category, SystemField.AssetType, SystemField.Territory } },
            { "Financials", new() { SystemField.Gross, SystemField.Net, SystemField.Amount, SystemField.Currency } },
            { "Asset Details", new() { SystemField.Asset, SystemField.Collection, SystemField.Artist, SystemField.Label, SystemField.Quantity } }
        };

    var categorySpecificGroups = new Dictionary<string, Dictionary<string, List<SystemField>>>
        {
            { "Merchandise", new() {
                { "Codes", new() { SystemField.Sku } },
                { "Specifics", new() { SystemField.Store, SystemField.CustomerEmail } }
            }},
            { "Royalties", new() {
                { "Codes", new() { SystemField.Isrc, SystemField.Upc } },
                { "Specifics", new() { SystemField.Dsp } }
            }},
            { "Concerts", new() {
                { "Specifics", new() { SystemField.VenueName, SystemField.EventStatus, SystemField.TicketClass } }
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
              .Add(Text.Label("Source Name (Provider)"))
              .Add(newTemplateSourceName.ToTextInput().Placeholder("e.g. Spotify, Distrokid, etc.")))
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
      var activeGroups = new Dictionary<string, List<SystemField>>(fieldGroups);

      if (categorySpecificGroups.TryGetValue(newTemplateCategory.Value, out var extraGroups))
      {
        foreach (var grp in extraGroups)
        {
          activeGroups[grp.Key] = grp.Value;
        }
      }

      var mapped = mappedPairs.Value;
      var unmappedHeaders = headers.Where(h => !mapped.Any(m => m.Header == h)).ToList();

      var allUnmappedOptions = new List<Option<SystemField?>>();
      foreach (var group in activeGroups)
      {
        var unmappedInGroup = group.Value.Where(f => !mapped.Any(m => m.FieldKey == f.ToString())).ToList();
        if (unmappedInGroup.Count == 0) continue;

        foreach (var field in unmappedInGroup)
        {
          var fieldInfo = field.GetType().GetField(field.ToString());
          var displayAttr = fieldInfo?.GetCustomAttribute<DisplayAttribute>();
          var label = displayAttr?.Name ?? field.ToString();
          allUnmappedOptions.Add(new Option<SystemField?>(label, field, group.Key));
        }
      }

      // Add state for dialog
      var showMappingDialog = UseState(false);

      var mapButton = new Button("Confirm Mapping", () =>
              {
                if (selectedHeaderToMap.Value != null && selectedFieldToMap.Value != null)
                {
                  var list = mappedPairs.Value.ToList();
                  list.Add((selectedHeaderToMap.Value, selectedFieldToMap.Value.Value.ToString()));
                  mappedPairs.Set(_ => list);
                  selectedHeaderToMap.Set((string?)null);
                  selectedFieldToMap.Set((SystemField?)null);
                }
              }).Variant(ButtonVariant.Primary)
                .Disabled(selectedHeaderToMap.Value == null || selectedFieldToMap.Value == null)
                .Icon(Icons.Link);

      var mappingDialog = showMappingDialog.Value ? new Dialog(
          _ => showMappingDialog.Set(false),
          new DialogHeader("Mapped Columns"),
          new DialogBody(
             Layout.Vertical().Padding(15).Gap(5)
              .Add(mapped.Count == 0 ?
                  Layout.Horizontal().Align(Align.Center).Add(Text.Muted("No columns mapped yet."))
                  : Layout.Vertical().Gap(5).Add(mapped.Select(m =>
                      Layout.Horizontal().Gap(10).Align(Align.Center).Padding(5)
                          .Add(new Icon(Icons.Check).Size(16))
                          .Add(Text.Label($"{m.Header}"))
                          .Add(new Icon(Icons.ArrowRight).Size(14))
                          .Add(Text.Label($"{m.FieldKey}"))
                          .Add(Layout.Horizontal().Grow())
                          .Add(new Button("", () =>
                          {
                            var list = mappedPairs.Value.ToList();
                            list.RemoveAll(x => x.Header == m.Header && x.FieldKey == m.FieldKey);
                            mappedPairs.Set(_ => list);
                          }).Variant(ButtonVariant.Ghost).Icon(Icons.Trash).Size(24))
                  ).ToArray())
              )
          ),
          new DialogFooter(new Button("Close", () => showMappingDialog.Set(false)))
      ) : null;

      content
          .Add(Layout.Horizontal().Align(Align.Center).Add(Text.H4("Step 2: Map Columns")))
          .Add(Layout.Horizontal().Gap(20).Width(Size.Full())
              .Add(new Card(
                  Layout.Vertical().Gap(5)
                      .Add(Text.H5("1. Select File Header"))
                      .Add(selectedHeaderToMap.ToSelectInput(unmappedHeaders.Select(h => new Option<string?>(h, h)))
                          .Variant(SelectInputs.List))
              ).Width(Size.Fraction(0.5f)))
              .Add(new Card(
                  Layout.Vertical().Gap(5)
                      .Add(Text.H5("2. Select System Field"))
                      .Add(selectedFieldToMap.ToSelectInput(allUnmappedOptions)
                          .Variant(SelectInputs.Select)
                          .Placeholder("Choose system field..."))
              ).Width(Size.Fraction(0.5f)))
          )
          .Add(Layout.Horizontal().Align(Align.Center).Padding(10)
              .Add(mapButton)
          )
          .Add(Layout.Horizontal().Gap(10).Align(Align.Right).Padding(10, 0, 0, 0)
              .Add(new Button("View Mapped Columns", () => showMappingDialog.Set(true))
                  .Variant(ButtonVariant.Outline).Icon(Icons.List))
              .Add(new Button("Back", () => templateCreationStep.Set(1)).Variant(ButtonVariant.Ghost).Icon(Icons.ArrowLeft))
              .Add(new Button("Save Template", async () =>
              {
                await using var db = factory.CreateDbContext();
                var newT = new ImportTemplate
                {
                  Name = newTemplateName.Value,
                  SourceName = newTemplateSourceName.Value,
                  Category = newTemplateCategory.Value,
                  HeadersJson = JsonSerializer.Serialize(headers),
                  AssetColumn = getHeader("Asset"),
                  AmountColumn = getHeader("Amount"),
                  CollectionColumn = getHeader("Collection"),
                  GrossColumn = getHeader("Gross"),
                  CurrencyColumn = getHeader("Currency"),
                  AssetTypeColumn = getHeader("AssetType"),
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

                queryService.RevalidateByTag("ImportTemplates");

                client.Toast("Template Created", "Success");
                _onSuccess();
              }).Variant(ButtonVariant.Primary).Disabled(mapped.Count == 0))
          );

      // Return fragment to support dialog
      return new Fragment(
        new Sheet(
            _ => { _onCancel(); return ValueTask.CompletedTask; },
            content,
            "Create Import Template",
            "Define how to import this file format."
        ).Width(Size.Full()),
        mappingDialog
      );
    }

    // Fallback for Step 1
    return new Sheet(
        _ => { _onCancel(); return ValueTask.CompletedTask; },
        content,
        "Create Import Template",
        "Define how to import this file format."
    ).Width(Size.Full());
  }
}
