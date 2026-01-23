using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Dynamic;
using ExcelDataReader;
using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;

namespace ExcelDataReaderExample;

public class ExcelDataReaderSheet(Action onClose) : ViewBase
{
  private readonly Action _onClose = onClose;

  private enum AnalyzerMode
  {
    Home,
    Analysis,
    TemplateCreation,
    Annex,
    DataView,
    Preview,
    Upload
  }

  public record FileAnalysis
  {
    public string FileName { get; set; } = "";
    public string FileType { get; set; } = "";
    public long FileSize { get; set; }
    public int TotalSheets { get; set; }
    public List<SheetAnalysis> Sheets { get; set; } = new();
  }

  public record SheetAnalysis
  {
    public string Name { get; set; } = "";
    public int FieldCount { get; set; }
    public int RowCount { get; set; }
    public List<string> Headers { get; set; } = new();
  }

  public record CurrentFile
  {
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Path { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public long FileSize { get; set; }
    public FileAnalysis? Analysis { get; set; }
    public ImportTemplate? MatchedTemplate { get; set; }
    public bool IsNewTemplate { get; set; }
    public List<Dictionary<string, object?>>? ParsedData { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Analyzing, Ready, Error
  }

  public override object? Build()
  {
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();

    // --- Global State ---
    var activeMode = UseState(AnalyzerMode.Home);
    var templates = UseState<List<ImportTemplate>>([]);

    // --- Analysis State ---
    // --- Analysis State ---
    var filePaths = UseState<List<CurrentFile>>(() => new());
    var isAnalyzing = UseState(false);
    // var matchedTemplate = UseState<ImportTemplate?>(() => null); // Moved to CurrentFile
    // var isNewTemplate = UseState(false); // Moved to CurrentFile
    // var parsedData = UseState<List<Dictionary<string, object?>>>([]); // Moved to CurrentFile

    // --- Annex State ---
    var annexEntries = UseState<List<RevenueEntry>>([]);

    var annexSelectedId = UseState<int?>(() => null); // Bound to SelectInput
    var annexTitle = UseState("");

    // --- Template Creation State ---
    var newTemplateName = UseState("");
    var newTemplateCategory = UseState("Merchandise");
    var templateCreationStep = UseState(1);
    var newTemplateAssetColumn = UseState<string?>(() => null);

    var newTemplateAmountColumn = UseState<string?>(() => null);
    var newTemplateCollectionColumn = UseState<string?>(() => null);
    var newTemplateGrossColumn = UseState<string?>(() => null);

    var newTemplateCurrencyColumn = UseState<string?>(() => null);

    // New Mappings
    var newTemplateTerritoryColumn = UseState<string?>(() => null);
    var newTemplateLabelColumn = UseState<string?>(() => null);
    var newTemplateArtistColumn = UseState<string?>(() => null);
    var newTemplateStoreColumn = UseState<string?>(() => null);
    var newTemplateDspColumn = UseState<string?>(() => null);
    var newTemplateNetColumn = UseState<string?>(() => null);
    var newTemplateTransactionDateColumn = UseState<string?>(() => null);
    var newTemplateTransactionIdColumn = UseState<string?>(() => null);
    var newTemplateSourcePlatformColumn = UseState<string?>(() => null);
    var newTemplateCategoryColumn = UseState<string?>(() => null); // Global header Category
    var newTemplateQuantityColumn = UseState<string?>(() => null);

    // Category Specific
    var newTemplateSkuColumn = UseState<string?>(() => null);
    var newTemplateCustomerEmailColumn = UseState<string?>(() => null);
    var newTemplateIsrcColumn = UseState<string?>(() => null);
    var newTemplateUpcColumn = UseState<string?>(() => null);
    var newTemplateVenueNameColumn = UseState<string?>(() => null);
    var newTemplateEventStatusColumn = UseState<string?>(() => null);
    var newTemplateTicketClassColumn = UseState<string?>(() => null);

    var selectedFieldKeys = UseState<List<string>>(() => new List<string>());

    // --- Upload/Save State ---
    var uploadName = UseState("");
    var uploadLinkId = UseState<int?>(() => null);

    // --- Smart Naming State (Royalties) ---
    var uploadYear = UseState(DateTime.Now.Year);
    var uploadQuarter = UseState("Q1");

    // Auto-name file based on Royalties selection
    UseEffect(() =>
    {
      if (activeMode.Value == AnalyzerMode.Upload)
      {
        var firstT = filePaths.Value.FirstOrDefault()?.MatchedTemplate;
        if (firstT?.Category == "Royalties")
        {
          uploadName.Set($"{uploadYear.Value} {uploadQuarter.Value} {firstT.Name}");
        }
      }
    }, [activeMode, filePaths, uploadYear, uploadQuarter]);

    // --- Upload Logic ---
    var uploadState = UseState<FileUpload<byte[]>?>();
    var uploadContext = this.UseUpload(MemoryStreamUploadHandler.Create(uploadState))
        .Accept(".xlsx,.xls,.csv")
        .MaxFileSize(50 * 1024 * 1024);

    // Load templates on mount
    UseEffect(async () =>
    {
      try
      {
        await using var db = factory.CreateDbContext();
        var loaded = await db.ImportTemplates.ToListAsync();
        templates.Set(loaded);
      }
      catch { }
    }, []);

    // Handle File Upload
    UseEffect(() =>
    {
      if (uploadState.Value?.Content is byte[] bytes && bytes.Length > 0)
      {
        try
        {
          var tempPath = System.IO.Path.GetTempFileName();
          var extension = ".xlsx";
          if (bytes.Length >= 4)
          {
            if (bytes[0] == 0x50 && bytes[1] == 0x4B) extension = ".xlsx";
            else if (bytes[0] == 0xD0 && bytes[1] == 0xCF) extension = ".xls";
            else
            {
              var content = System.Text.Encoding.UTF8.GetString(bytes.Take(100).ToArray());
              if (content.Contains(',')) extension = ".csv";
            }
          }
          var finalPath = tempPath + extension;
          File.WriteAllBytes(finalPath, bytes);

          var newFile = new CurrentFile
          {
            Path = finalPath,
            OriginalName = uploadState.Value.FileName ?? "Unknown",
            FileSize = bytes.Length
          };

          var list = filePaths.Value.ToList();
          list.Add(newFile);
          filePaths.Set(list);

          // Reset upload state to allow adding more? 
          // Actually, if I reset it immediately, it might re-trigger if not careful.
          // But usually we want to clear it so the input is clear.
        }
        catch (Exception ex)
        {
          try { client.Toast($"File upload error: {ex.Message}", "Error"); } catch { }
        }
      }
    }, [uploadState]);

    // Analysis Logic
    UseEffect(async () =>
    {
      if (isAnalyzing.Value) return;

      var files = filePaths.Value;
      var pending = files.FirstOrDefault(f => f.Status == "Pending");

      if (pending != null)
      {
        isAnalyzing.Set(true);

        FileAnalysis? result = null;
        try
        {
          result = await Task.Run(() =>
              {
                try { return AnalyzeFile(pending.Path); }
                catch { return null; }
              });
        }
        catch { }

        // Update the file in the list
        // Note: Re-fetch list to avoid closure staleness if possible, though UseEffect should have latest
        var currentList = filePaths.Value.ToList();
        var index = currentList.FindIndex(f => f.Id == pending.Id);

        if (index != -1)
        {
          var updated = currentList[index] with
          {
            Status = result != null ? "Analyzed" : "Error",
            Analysis = result
          };

          // Match template
          if (result?.Sheets.Count > 0)
          {
            var firstSheetHeaders = result.Sheets[0].Headers;
            var jsonHeaders = JsonSerializer.Serialize(firstSheetHeaders);
            var match = templates.Value.FirstOrDefault(t => t.HeadersJson == jsonHeaders);

            updated = updated with
            {
              MatchedTemplate = match,
              IsNewTemplate = match == null
            };
          }

          currentList[index] = updated;
          filePaths.Set(currentList);
        }

        isAnalyzing.Set(false);
      }
    }, [filePaths, templates]);

    // Load Annex Entries only when in Annex Mode
    UseEffect(() => Task.Run(async () =>
    {
      if (activeMode.Value == AnalyzerMode.Annex && annexEntries.Value.Count == 0)
      {
        await using var db = factory.CreateDbContext();
        var results = await db.RevenueEntries
               .Include(e => e.Source)
               .OrderBy(e => e.Id)
               .Take(1000)
               .ToListAsync();
        annexEntries.Set(results);
      }
    }), [activeMode]);

    // --- Helpers ---


    // We need a way to parse all rows for a file
    List<Dictionary<string, object?>> ParseFileRows(CurrentFile file)
    {
      if (file.Path == null || file.MatchedTemplate == null) return [];
      return ParseData(file.Path, file.MatchedTemplate);
    }

    List<Dictionary<string, object?>> ParseCurrentFile()
    {
      // Legacy helper used by Preview/Upload single item logic
      // We'll use the FIRST file for preview
      var first = filePaths.Value.FirstOrDefault();
      if (first != null) return ParseFileRows(first);
      return [];
    }

    CurrentFile? GetActiveFile() => filePaths.Value.FirstOrDefault(); // Helper for views expecting single file context

    // --- Render Methods ---

    object RenderHome()
    {
      var files = filePaths.Value;
      var hasFiles = files.Count > 0;
      var isProcessing = isAnalyzing.Value;
      var ready = files.All(f => f.Status == "Analyzed");

      // Determine if we show a dialog
      // For now, if we are in a sub-mode, we show it. 
      // But sub-modes like "Preview" need a target file.
      // We will assume "Preview" previews the FIRST file for now.
      var showDialog = (activeMode.Value != AnalyzerMode.Home && activeMode.Value != AnalyzerMode.Upload)
                       || (activeMode.Value == AnalyzerMode.Upload && ready); // Upload sub-mode logic

      string GetDialogTitle() => activeMode.Value switch
      {
        AnalyzerMode.TemplateCreation => "Create Import Template",
        AnalyzerMode.Analysis => "File Metadata",
        AnalyzerMode.Annex => "Annex Data",
        AnalyzerMode.DataView => "Data Preview",
        AnalyzerMode.Preview => "Preview File",
        AnalyzerMode.Upload => "Upload Table",
        _ => "Import Data"
      };

      return new Sheet(
          _ => { _onClose(); return ValueTask.CompletedTask; },
          Layout.Vertical().Height(Size.Full()).Width(Size.Full()).Align(Align.Center).Padding(20)
            .Add(
                Layout.Vertical()
                .Gap(20)
                .Align(Align.Center)
                .Add(new Icon(Icons.Sheet).Size(48))
                .Add(Text.H4("Bulk Import"))
                .Add(uploadState.ToFileInput(uploadContext).Placeholder("Select Files (Excel/CSV)").Width(300))
                .Add(isProcessing ? Text.Label("Processing...") : null)
                .Add(hasFiles ?
                    Layout.Vertical().Gap(10).Width(Size.Full())
                    .Add(files.Select(f =>
                        Layout.Horizontal().Gap(10).Align(Align.Center).Padding(10) // Fixed Class error
                        .Add(new Icon(Icons.FileText).Size(20))
                        .Add(Layout.Vertical()
                            .Add(Text.Label(f.OriginalName))
                            .Add(Text.Muted($"{FormatFileSize(f.FileSize)} - {f.Status}")) // Fixed Tiny error
                        )
                        .Add(Layout.Horizontal().Grow()) // Fixed Spacer error
                        .Add(f.MatchedTemplate != null ? Text.Success(f.MatchedTemplate.Name) :
                             f.Status == "Analyzed" ? Text.Warning("No Template") : Text.Muted("-"))
                        .Add(new Button("", () =>
                        {
                          var l = filePaths.Value.ToList();
                          l.RemoveAll(x => x.Id == f.Id);
                          filePaths.Set(l);
                        }).Variant(ButtonVariant.Ghost).Icon(Icons.Trash))
                    ).ToArray())
                    : null
                )
                .Add(hasFiles && ready && !isProcessing ?
                    Layout.Horizontal().Gap(10)
                    .Add(new Button("Clear All", () => filePaths.Set(new List<CurrentFile>())).Variant(ButtonVariant.Outline))
                    .Add(new Button("Import All", () =>
                    {
                      // Check if all work
                      if (files.Any(f => f.MatchedTemplate == null))
                      {
                        client.Toast("Templates missing for some files", "Warning");
                        return;
                      }
                      uploadName.Set($"Batch Import {DateTime.Now}");
                      activeMode.Set(AnalyzerMode.Upload);
                    }).Variant(ButtonVariant.Primary))
                    : null
                )
                .Add(hasFiles && files.Any(f => f.Status == "Analyzed" && f.MatchedTemplate == null) ?
                     new Button("Create Template", () => activeMode.Set(AnalyzerMode.TemplateCreation))
                     .Variant(ButtonVariant.Secondary)
                     : null
                )
             )
             .Add(showDialog ? new Dialog(
              _ =>
              {
                if (activeMode.Value != AnalyzerMode.Home)
                {
                  activeMode.Set(AnalyzerMode.Home);
                }
                else
                {
                  // Clear logic?
                }
              },
              new DialogHeader(GetDialogTitle()),
              new DialogBody(
                   activeMode.Value == AnalyzerMode.TemplateCreation ? RenderTemplateCreationContent() :
                   activeMode.Value == AnalyzerMode.Analysis ? RenderAnalysisContent() :
                   activeMode.Value == AnalyzerMode.Annex ? RenderAnnexContent() :
                   activeMode.Value == AnalyzerMode.DataView ? RenderDataTableView() :
                   activeMode.Value == AnalyzerMode.Preview ? RenderPreviewContent() :
                   activeMode.Value == AnalyzerMode.Upload ? RenderUploadContent() :
                   Text.Muted("Not implemented view")
              ),
              new DialogFooter()
            ) : null),
          "Excel Bulk Import",
          "Upload and process multiple financial data files."
      );
    }

    object RenderAnalysisContent()
    {
      var fa = GetActiveFile()?.Analysis;
      if (fa == null) return Text.Muted("No analysis available.");

      return Layout.Vertical().Gap(10).Width(Size.Full())
           | new Button("Back", () => activeMode.Set(AnalyzerMode.Home)).Variant(ButtonVariant.Link).Icon(Icons.ArrowLeft)
           | new Markdown($"""                                
                             | Property | Value |
                             |----------|-------|
                             | **File name** | `{fa.FileName}` |
                             | **Type** | `{fa.FileType}` |
                             | **Size** | `{FormatFileSize(fa.FileSize)}` |
                             """)
           | Layout.Vertical(
               fa.Sheets.Select((sheet, index) =>
                       new Expandable(
                           Text.Label($"Sheet {index + 1}: {sheet.Name}"),
                           new Markdown($"""
                                         | Property | Value |
                                         |----------|-------|
                                         | **Columns** | `{sheet.FieldCount}` |
                                         | **Rows** | `{sheet.RowCount}` |
                                         | **Headers** | {string.Join(", ", sheet.Headers.Select(header => $"`{header}`"))} |
                                         """)
                       )
               ).ToArray()
           );
    }

    object RenderTemplateCreationContent()
    {
      var analysis = GetActiveFile()?.Analysis;
      if (analysis?.Sheets.Count == 0) return Text.Muted("No sheets found in first file.");
      var headers = analysis!.Sheets[0].Headers;

      var globalFields = new Dictionary<string, string>
      {
        { "TransactionDate", "Transaction Date" },
        { "TransactionId", "Transaction ID" },
        { "SourcePlatform", "Source Platform" },
        { "Asset", "Asset Name (Item)" },
        { "Collection", "Asset Group (Parent)" },
        { "Category", "Category" },
        { "Quantity", "Quantity" },
        { "Gross", "Gross Revenue" },
        { "Net", "Net Revenue" },
        { "Currency", "Currency" },
        { "Territory", "Territory/Region" }
      };

      var categoryFields = new Dictionary<string, Dictionary<string, string>>
      {
        { "Merchandise", new Dictionary<string, string> { { "Sku", "SKU" }, { "CustomerEmail", "Customer Email" }, { "Store", "Store" } } },
        { "Royalties", new Dictionary<string, string> { { "Isrc", "ISRC" }, { "Upc", "UPC" }, { "Dsp", "DSP" }, { "Artist", "Artist" }, { "Label", "Label" } } },
        { "Concerts", new Dictionary<string, string> { { "VenueName", "Venue Name" }, { "EventStatus", "Event Status" }, { "TicketClass", "Ticket Class" } } },
        { "Other", new Dictionary<string, string>() }
      };

      if (templateCreationStep.Value == 1)
      {
        return Layout.Vertical().Gap(10).Width(Size.Full())
            | Text.H4("Step 1: Template Details")
            | Layout.Vertical().Gap(2)
                | Text.Label("Template Name")
                | newTemplateName.ToTextInput().Placeholder("e.g. Spotify Report")
            | Layout.Vertical().Gap(2)
                | Text.Label("Category")
                | newTemplateCategory.ToSelectInput(new[] { "Merchandise", "Royalties", "Concerts", "Other" }.Select(c => new Option<string>(c, c)))
            | Layout.Horizontal().Align(Align.Right).Padding(10, 0, 0, 0)
                | new Button("Next", () =>
                {
                  if (string.IsNullOrWhiteSpace(newTemplateName.Value)) { client.Toast("Name required", "Warning"); return; }
                  // Initialize selected fields with basic globals
                  selectedFieldKeys.Set(new List<string> { "Asset", "Net", "Currency" });
                  templateCreationStep.Set(2);
                }).Variant(ButtonVariant.Primary).Icon(Icons.ArrowRight);
      }
      else if (templateCreationStep.Value == 2)
      {
        var availableFields = new Dictionary<string, string>(globalFields);
        if (categoryFields.TryGetValue(newTemplateCategory.Value, out var extra))
        {
          foreach (var kv in extra) availableFields[kv.Key] = kv.Value;
        }

        var options = availableFields.Select(kv => new Option<string>(kv.Value, kv.Key)).ToList();

        return Layout.Vertical().Gap(10).Width(Size.Full())
            | Layout.Horizontal().Align(Align.Center).Gap(10)
                | new Button("", () => templateCreationStep.Set(1)).Variant(ButtonVariant.Ghost).Icon(Icons.ArrowLeft)
                | Text.H4("Step 2: Select Fields")
            | Text.Muted($"Select fields to map for {newTemplateCategory.Value}.")
            | Layout.Vertical().Gap(5)
                | Text.Label("Standard Fields")
                | selectedFieldKeys.ToSelectInput(options).Placeholder("Select fields...")
            | Layout.Horizontal().Align(Align.Right).Padding(10, 0, 0, 0)
                | new Button("Next", () =>
                {
                  if (selectedFieldKeys.Value.Count == 0) { client.Toast("Select at least one field", "Warning"); return; }
                  templateCreationStep.Set(3);
                }).Variant(ButtonVariant.Primary).Icon(Icons.ArrowRight);
      }
      else
      {
        // Step 3: Mapping
        var selectedFieldsMap = new Dictionary<string, string>();
        foreach (var key in selectedFieldKeys.Value)
        {
          if (globalFields.TryGetValue(key, out var label)) selectedFieldsMap[key] = label;
          else if (categoryFields.TryGetValue(newTemplateCategory.Value, out var extras) && extras.TryGetValue(key, out label)) selectedFieldsMap[key] = label;
        }

        return Layout.Vertical().Gap(10).Width(Size.Full())
            | Layout.Horizontal().Align(Align.Center).Gap(10)
                | new Button("", () => templateCreationStep.Set(2)).Variant(ButtonVariant.Ghost).Icon(Icons.ArrowLeft)
                | Text.H4("Step 3: Map Columns")
            | Text.Muted("Assign file headers to your selected fields.")
            | headers.Select(h =>
            {
              var roleKey = GetRoleKeyForHeader(h);
              var currentRoleKey = roleKey ?? "Ignore";
              var currentRoleLabel = currentRoleKey == "Ignore" ? "Ignore" : (selectedFieldsMap.TryGetValue(currentRoleKey, out var l) ? l : currentRoleKey);

              var menu = new DropDownMenu(
                  DropDownMenu.DefaultSelectHandler(),
                  new Button(currentRoleLabel).Variant(ButtonVariant.Outline).Icon(Icons.ChevronDown).Width(Size.Full())
              );

              menu = menu | MenuItem.Default("Ignore").HandleSelect(() => ClearColumn(h));

              foreach (var field in selectedFieldsMap)
              {
                menu = menu | MenuItem.Default(field.Value).HandleSelect(() => { ClearColumn(h); SetColumn(field.Key, h); });
              }

              return Layout.Horizontal().Gap(10).Align(Align.Center)
                  | Layout.Vertical().Width(Size.Fraction(1)).Align(Align.Left)
                      | Text.H5(h)
                  | Layout.Vertical().Width(180)
                      | menu;
            }).ToArray()
            | new Button("Save Template", async () =>
            {
              await using var db = factory.CreateDbContext();
              var newT = new ImportTemplate
              {
                Name = newTemplateName.Value,
                Category = newTemplateCategory.Value,
                HeadersJson = JsonSerializer.Serialize(headers),

                // Global
                AssetColumn = newTemplateAssetColumn.Value,
                AmountColumn = newTemplateAmountColumn.Value,
                CollectionColumn = newTemplateCollectionColumn.Value,
                GrossColumn = newTemplateGrossColumn.Value,
                CurrencyColumn = newTemplateCurrencyColumn.Value,
                TerritoryColumn = newTemplateTerritoryColumn.Value,
                LabelColumn = newTemplateLabelColumn.Value,
                ArtistColumn = newTemplateArtistColumn.Value,
                StoreColumn = newTemplateStoreColumn.Value,
                DspColumn = newTemplateDspColumn.Value,
                NetColumn = newTemplateNetColumn.Value ?? newTemplateAmountColumn.Value,

                TransactionDateColumn = newTemplateTransactionDateColumn.Value,
                TransactionIdColumn = newTemplateTransactionIdColumn.Value,
                SourcePlatformColumn = newTemplateSourcePlatformColumn.Value,
                CategoryColumn = newTemplateCategoryColumn.Value,
                QuantityColumn = newTemplateQuantityColumn.Value,

                // Category Specific
                SkuColumn = newTemplateSkuColumn.Value,
                CustomerEmailColumn = newTemplateCustomerEmailColumn.Value,
                IsrcColumn = newTemplateIsrcColumn.Value,
                UpcColumn = newTemplateUpcColumn.Value,
                VenueNameColumn = newTemplateVenueNameColumn.Value,
                EventStatusColumn = newTemplateEventStatusColumn.Value,
                TicketClassColumn = newTemplateTicketClassColumn.Value,

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
              };
              db.ImportTemplates.Add(newT);
              await db.SaveChangesAsync();

              var fresh = await db.ImportTemplates.ToListAsync();
              templates.Set(fresh);

              // Removed matchedTemplate.Set and isNewTemplate.Set as they are no longer relevant 
              // for the global state in multi-file context. 
              // The updated templates list will trigger re-analysis in the UseEffect hook.

              activeMode.Set(AnalyzerMode.Home);
              templateCreationStep.Set(1);
              client.Toast("Template Created", "Success");
            }).Variant(ButtonVariant.Primary).Width(Size.Full());
      }
    }

    string? GetRoleKeyForHeader(string header)
    {
      if (newTemplateAssetColumn.Value == header) return "Asset";
      if (newTemplateAmountColumn.Value == header) return "Net";
      if (newTemplateCollectionColumn.Value == header) return "Collection";
      if (newTemplateGrossColumn.Value == header) return "Gross";
      if (newTemplateCurrencyColumn.Value == header) return "Currency";
      if (newTemplateTerritoryColumn.Value == header) return "Territory";
      if (newTemplateLabelColumn.Value == header) return "Label";
      if (newTemplateArtistColumn.Value == header) return "Artist";
      if (newTemplateStoreColumn.Value == header) return "Store";
      if (newTemplateDspColumn.Value == header) return "Dsp";
      if (newTemplateNetColumn.Value == header) return "Net";
      if (newTemplateTransactionDateColumn.Value == header) return "TransactionDate";
      if (newTemplateTransactionIdColumn.Value == header) return "TransactionId";
      if (newTemplateSourcePlatformColumn.Value == header) return "SourcePlatform";
      if (newTemplateCategoryColumn.Value == header) return "Category";
      if (newTemplateQuantityColumn.Value == header) return "Quantity";
      if (newTemplateSkuColumn.Value == header) return "Sku";
      if (newTemplateCustomerEmailColumn.Value == header) return "CustomerEmail";
      if (newTemplateIsrcColumn.Value == header) return "Isrc";
      if (newTemplateUpcColumn.Value == header) return "Upc";
      if (newTemplateVenueNameColumn.Value == header) return "VenueName";
      if (newTemplateEventStatusColumn.Value == header) return "EventStatus";
      if (newTemplateTicketClassColumn.Value == header) return "TicketClass";
      return null;
    }

    void SetColumn(string key, string header)
    {
      switch (key)
      {
        case "Asset": newTemplateAssetColumn.Set(header); break;
        case "Net": newTemplateAmountColumn.Set(header); break;
        case "Collection": newTemplateCollectionColumn.Set(header); break;
        case "Gross": newTemplateGrossColumn.Set(header); break;
        case "Currency": newTemplateCurrencyColumn.Set(header); break;
        case "Territory": newTemplateTerritoryColumn.Set(header); break;
        case "Label": newTemplateLabelColumn.Set(header); break;
        case "Artist": newTemplateArtistColumn.Set(header); break;
        case "Store": newTemplateStoreColumn.Set(header); break;
        case "Dsp": newTemplateDspColumn.Set(header); break;
        case "TransactionDate": newTemplateTransactionDateColumn.Set(header); break;
        case "TransactionId": newTemplateTransactionIdColumn.Set(header); break;
        case "SourcePlatform": newTemplateSourcePlatformColumn.Set(header); break;
        case "Category": newTemplateCategoryColumn.Set(header); break;
        case "Quantity": newTemplateQuantityColumn.Set(header); break;
        case "Sku": newTemplateSkuColumn.Set(header); break;
        case "CustomerEmail": newTemplateCustomerEmailColumn.Set(header); break;
        case "Isrc": newTemplateIsrcColumn.Set(header); break;
        case "Upc": newTemplateUpcColumn.Set(header); break;
        case "VenueName": newTemplateVenueNameColumn.Set(header); break;
        case "EventStatus": newTemplateEventStatusColumn.Set(header); break;
        case "TicketClass": newTemplateTicketClassColumn.Set(header); break;
      }
    }

    void ClearColumn(string h)
    {
      if (newTemplateAssetColumn.Value == h) newTemplateAssetColumn.Set((string?)null);
      if (newTemplateAmountColumn.Value == h) newTemplateAmountColumn.Set((string?)null);
      if (newTemplateCollectionColumn.Value == h) newTemplateCollectionColumn.Set((string?)null);
      if (newTemplateGrossColumn.Value == h) newTemplateGrossColumn.Set((string?)null);
      if (newTemplateCurrencyColumn.Value == h) newTemplateCurrencyColumn.Set((string?)null);
      if (newTemplateTerritoryColumn.Value == h) newTemplateTerritoryColumn.Set((string?)null);
      if (newTemplateLabelColumn.Value == h) newTemplateLabelColumn.Set((string?)null);
      if (newTemplateArtistColumn.Value == h) newTemplateArtistColumn.Set((string?)null);
      if (newTemplateStoreColumn.Value == h) newTemplateStoreColumn.Set((string?)null);
      if (newTemplateDspColumn.Value == h) newTemplateDspColumn.Set((string?)null);
      if (newTemplateNetColumn.Value == h) newTemplateNetColumn.Set((string?)null);
      if (newTemplateTransactionDateColumn.Value == h) newTemplateTransactionDateColumn.Set((string?)null);
      if (newTemplateTransactionIdColumn.Value == h) newTemplateTransactionIdColumn.Set((string?)null);
      if (newTemplateSourcePlatformColumn.Value == h) newTemplateSourcePlatformColumn.Set((string?)null);
      if (newTemplateCategoryColumn.Value == h) newTemplateCategoryColumn.Set((string?)null);
      if (newTemplateQuantityColumn.Value == h) newTemplateQuantityColumn.Set((string?)null);
      if (newTemplateSkuColumn.Value == h) newTemplateSkuColumn.Set((string?)null);
      if (newTemplateCustomerEmailColumn.Value == h) newTemplateCustomerEmailColumn.Set((string?)null);
      if (newTemplateIsrcColumn.Value == h) newTemplateIsrcColumn.Set((string?)null);
      if (newTemplateUpcColumn.Value == h) newTemplateUpcColumn.Set((string?)null);
      if (newTemplateVenueNameColumn.Value == h) newTemplateVenueNameColumn.Set((string?)null);
      if (newTemplateEventStatusColumn.Value == h) newTemplateEventStatusColumn.Set((string?)null);
      if (newTemplateTicketClassColumn.Value == h) newTemplateTicketClassColumn.Set((string?)null);
    }




    object RenderAnnexContent()
    {
      var activeFile = GetActiveFile();
      var tmpl = activeFile?.MatchedTemplate;

      var options = annexEntries.Value.Select(e =>
         new Option<int?>($"{e.Description ?? "No Name"} - {e.RevenueDate:MM/dd/yyyy} - {e.Source.DescriptionText} - {e.Amount:C}", e.Id)
      ).ToList();

      return Layout.Vertical().Gap(5).Width(Size.Full())
           | new Button("Back", () => activeMode.Set(AnalyzerMode.Home)).Variant(ButtonVariant.Primary).Icon(Icons.ArrowLeft)
           | Text.Muted("Attach the current file content to an existing Revenue Entry.")
           | Layout.Vertical().Gap(5)
               | Text.Label("Entry Title")
               | annexTitle.ToTextInput().Placeholder("e.g. Q1 Royalty Statement")
           | Layout.Vertical().Gap(5)
               | Text.Label("Template Used")
               | Text.Muted(tmpl?.Name ?? "Unknown")
           | Layout.Vertical().Gap(5)
               | "Target Entry"
               | annexSelectedId.ToSelectInput(options).Placeholder("Search entry...")
           | new Button("Attach Data", async () =>
           {
             if (annexSelectedId.Value == null) { client.Toast("Select an entry", "Warning"); return; }

             // We only annex the FIRST/Active file for now
             var data = ParseCurrentFile();
             if (data.Count == 0) { client.Toast("No data to attach", "Warning"); return; }

             await using var db = factory.CreateDbContext();
             var entry = await db.RevenueEntries.FindAsync(annexSelectedId.Value);
             if (entry != null)
             {
               // Load existing data
               var existingSheets = new List<object>();
               if (!string.IsNullOrEmpty(entry.JsonData))
               {
                 try
                 {
                   using var doc = JsonDocument.Parse(entry.JsonData);
                   if (doc.RootElement.ValueKind == JsonValueKind.Array)
                   {
                     if (doc.RootElement.GetArrayLength() > 0)
                     {
                       var first = doc.RootElement[0];
                       if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("FileName", out _))
                       {
                         existingSheets = JsonSerializer.Deserialize<List<object>>(entry.JsonData) ?? [];
                       }
                       else
                       {
                         var rows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(entry.JsonData);
                         existingSheets.Add(new { Title = "Legacy Data", FileName = "Legacy", TemplateName = "Unknown", Rows = rows });
                       }
                     }
                   }
                   else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                   {
                     var obj = JsonSerializer.Deserialize<object>(entry.JsonData);
                     if (obj != null) existingSheets.Add(obj);
                   }
                 }
                 catch { }
               }

               // Append new sheet
               var payload = new
               {
                 Title = annexTitle.Value,
                 FileName = activeFile?.Analysis?.FileName ?? "Unknown File",
                 TemplateName = tmpl?.Name ?? "Unknown Template",
                 Rows = data
               };
               existingSheets.Add(payload);

               entry.JsonData = JsonSerializer.Serialize(existingSheets);

               // --- Asset Extraction Logic ---
               if (tmpl != null && !string.IsNullOrEmpty(tmpl.AssetColumn) && !string.IsNullOrEmpty(tmpl.AmountColumn))
               {
                 try
                 {
                   var assetCol = tmpl.AssetColumn;
                   var amountCol = tmpl.AmountColumn;
                   var collectionCol = tmpl.CollectionColumn;

                   var batchAssets = new Dictionary<string, decimal>();
                   var assetCollections = new Dictionary<string, string>();

                   foreach (var row in data)
                   {
                     if (row.TryGetValue(assetCol, out var nameObj) && row.TryGetValue(amountCol, out var amountObj))
                     {
                       var name = nameObj?.ToString()?.Trim();
                       if (!string.IsNullOrEmpty(name) && decimal.TryParse(amountObj?.ToString(), out var amount))
                       {
                         if (!batchAssets.ContainsKey(name))
                         {
                           batchAssets[name] = 0;
                           if (!string.IsNullOrEmpty(collectionCol) && row.TryGetValue(collectionCol, out var colObj))
                           {
                             var colStr = colObj?.ToString()?.Trim();
                             if (!string.IsNullOrEmpty(colStr)) assetCollections[name] = colStr;
                           }
                         }
                         batchAssets[name] += amount;
                       }
                     }
                   }

                   if (batchAssets.Count > 0)
                   {
                     var names = batchAssets.Keys.ToList();
                     var existingAssets = await db.Assets.Where(a => names.Contains(a.Name)).ToListAsync();
                     var existingNames = existingAssets.Select(a => a.Name).ToHashSet();

                     var newAssets = names.Where(n => !existingNames.Contains(n))
                                          .Select(n => new Asset
                                          {
                                            Name = n,
                                            Type = "Unknown",
                                            Category = tmpl.Category,
                                            Collection = assetCollections.GetValueOrDefault(n) ?? "",
                                            AmountGenerated = 0
                                          })
                                          .ToList();

                     if (newAssets.Count > 0)
                     {
                       db.Assets.AddRange(newAssets);
                       await db.SaveChangesAsync();
                       existingAssets.AddRange(newAssets);
                     }

                     var assetMap = existingAssets.ToDictionary(a => a.Name, a => a.Id);
                     var revenueRecords = batchAssets.Select(kvp => new AssetRevenue
                     {
                       AssetId = assetMap[kvp.Key],
                       RevenueEntry = entry,
                       Amount = kvp.Value
                     });

                     db.AssetRevenues.AddRange(revenueRecords);
                   }
                 }
                 catch (Exception ex)
                 {
                   client.Toast($"Asset extraction failed: {ex.Message}", "Error");
                 }
               }

               await db.SaveChangesAsync();
               client.Toast($"Annexed to {entry.Description}", "Success");
               activeMode.Set(AnalyzerMode.Home);
               annexTitle.Set("");
             }
           }).Variant(ButtonVariant.Primary).Disabled(annexSelectedId.Value == null);
    }


    object RenderDataTableView(bool isEmbedded = false)
    {
      var activeFile = GetActiveFile();
      var tmpl = activeFile?.MatchedTemplate;
      var data = ParseCurrentFile();
      if (tmpl == null || data.Count == 0) return Text.Muted("No data loaded.");

      var headers = tmpl.GetHeaders();
      var dRows = data.Select(d =>
      {
        var r = new DynamicRow();
        int i = 0;
        foreach (var h in headers)
        {
          if (i >= 50) break;
          r.SetValue(i, d.GetValueOrDefault(h));
          i++;
        }
        return r;
      }).AsQueryable();

      var cols = headers.Select((h, i) => i < 50 ? new DataTableColumn
      {
        Name = $"Col{i}",
        Header = h,
        Order = i,
        Sortable = true,
        Filterable = true,
        ColType = ColType.Text
      } : null).Where(c => c != null).Cast<DataTableColumn>().ToArray();

      var config = new DataTableConfig
      {
        FreezeColumns = 1,
        ShowGroups = true,
        ShowIndexColumn = true,
        ShowSearch = true,
        AllowSorting = true,
        AllowFiltering = true,
        ShowVerticalBorders = true
      };

      if (isEmbedded)
      {
        return new DataTableView(dRows, Size.Full(), Size.Units(400), cols, config);
      }

      return Layout.Vertical().Gap(10).Width(Size.Full())
              | Layout.Horizontal().Gap(10).Align(Align.Center)
                  | new Button("Back", () => activeMode.Set(AnalyzerMode.Home)).Variant(ButtonVariant.Link).Icon(Icons.ArrowLeft)
                  | Text.H4($"{data.Count} Rows")
              | new DataTableView(dRows, Size.Full(), Size.Fit(), cols, config);
    }

    object RenderPreviewContent()
    {
      var activeFile = GetActiveFile();
      var fa = activeFile?.Analysis;
      if (fa == null) return Text.Muted("No analysis");

      var tmpl = activeFile?.MatchedTemplate;

      return Layout.Vertical().Gap(20).Width(Size.Full())
          | Layout.Horizontal().Gap(10).Align(Align.Center)
              | new Button("Back", () => activeMode.Set(AnalyzerMode.Home)).Variant(ButtonVariant.Link).Icon(Icons.ArrowLeft)
              | Text.H4($"Preview: {fa.FileName}")
          | (tmpl != null
              ? Text.Success($"Matched Template: {tmpl.Name} ({tmpl.Category})")
              : Text.Warning("No template matched"))
          | RenderDataTableView(true);
    }

    object RenderUploadContent()
    {
      var files = filePaths.Value.Where(f => f.Status == "Analyzed" && f.MatchedTemplate != null).ToList();
      if (files.Count == 0) return Text.Muted("No valid files to import.");

      return Layout.Vertical().Gap(20).Width(Size.Full())
           | Layout.Horizontal().Gap(10).Align(Align.Center)
               | new Button("Back", () => activeMode.Set(AnalyzerMode.Home)).Variant(ButtonVariant.Link).Icon(Icons.ArrowLeft)
               | Text.H4("Confirm Import")
           | Text.Label($"Ready to import {files.Count} files.")
           | Layout.Vertical().Gap(5).Add(files.Select(f => Text.Muted($"• {f.OriginalName} ({f.MatchedTemplate?.Name})")).ToArray())
           | Layout.Vertical().Gap(10)
               | Text.Label("Batch Name (Revenue Entry Description)")
               | uploadName.ToTextInput().Placeholder("e.g. 2024 Q1 Royalties")
           | Layout.Vertical().Gap(10)
               | "Smart Naming (Royalties)"
               | Layout.Horizontal().Gap(10)
                   | uploadYear.ToSelectInput(Enumerable.Range(2020, 10).Select(y => new Option<int>(y.ToString(), y))).Width(100)
                   | uploadQuarter.ToSelectInput(new[] { "Q1", "Q2", "Q3", "Q4" }.Select(q => new Option<string>(q, q))).Width(100)
           | new Button("Import Data", async () =>
           {
             if (string.IsNullOrWhiteSpace(uploadName.Value)) { client.Toast("Name required", "Warning"); return; }

             await using var db = factory.CreateDbContext();

             foreach (var file in files)
             {
               var jsonData = ParseFileRows(file);

               // Try to resolve source
               RevenueSource? source = await db.RevenueSources.FirstOrDefaultAsync(s => s.DescriptionText == "Excel Import");
               if (source == null)
               {
                 source = new RevenueSource { DescriptionText = "Excel Import" };
                 db.RevenueSources.Add(source);
                 await db.SaveChangesAsync();
               }

               // Ensure at least one artist exists
               var artist = await db.Artists.FirstOrDefaultAsync();
               if (artist == null)
               {
                 artist = new Artist { Name = "Unknown Artist", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                 db.Artists.Add(artist);
                 await db.SaveChangesAsync();
               }

               var entry = new RevenueEntry
               {
                 RevenueDate = DateTime.Now,
                 Description = $"{uploadName.Value} - {file.OriginalName}",
                 Amount = 0,
                 SourceId = source.Id,
                 ArtistId = artist.Id,
                 CreatedAt = DateTime.UtcNow,
                 UpdatedAt = DateTime.UtcNow,
                 JsonData = JsonSerializer.Serialize(jsonData)
               };

               var tmpl = file.MatchedTemplate!;
               decimal totalAmount = 0;
               if (!string.IsNullOrEmpty(tmpl.AmountColumn))
               {
                 foreach (var row in jsonData)
                 {
                   if (row.TryGetValue(tmpl.AmountColumn, out var val) && decimal.TryParse(val?.ToString(), out var d))
                     totalAmount += d;
                 }
               }
               entry.Amount = totalAmount;

               if (tmpl.Category == "Royalties")
               {
                 entry.Year = uploadYear.Value;
                 entry.Quarter = uploadQuarter.Value;
               }
               entry.ImportTemplateId = tmpl.Id;
               entry.FileName = file.OriginalName;

               db.RevenueEntries.Add(entry);
               await db.SaveChangesAsync(); // Save to get ID

               // Asset Logic
               if (!string.IsNullOrEmpty(tmpl.AssetColumn))
               {
                 try
                 {
                   var assetCol = tmpl.AssetColumn;
                   var amountCol = tmpl.AmountColumn; // Net
                   var collectionCol = tmpl.CollectionColumn;
                   var batchAssets = new Dictionary<string, decimal>();
                   var assetCollections = new Dictionary<string, string>();

                   foreach (var row in jsonData)
                   {
                     if (row.TryGetValue(assetCol, out var nameObj) && row.TryGetValue(amountCol, out var amountObj))
                     {
                       var name = nameObj?.ToString()?.Trim();
                       if (!string.IsNullOrEmpty(name) && decimal.TryParse(amountObj?.ToString(), out var amount))
                       {
                         if (!batchAssets.ContainsKey(name))
                         {
                           batchAssets[name] = 0;
                           if (!string.IsNullOrEmpty(collectionCol) && row.TryGetValue(collectionCol, out var colObj))
                           {
                             var colStr = colObj?.ToString()?.Trim();
                             if (!string.IsNullOrEmpty(colStr)) assetCollections[name] = colStr;
                           }
                         }
                         batchAssets[name] += amount;
                       }
                     }
                   }

                   if (batchAssets.Count > 0)
                   {
                     var names = batchAssets.Keys.ToList();
                     var existingAssets = await db.Assets.Where(a => names.Contains(a.Name)).ToListAsync();
                     var existingNames = existingAssets.Select(a => a.Name).ToHashSet();
                     var newAssets = names.Where(n => !existingNames.Contains(n))
                                          .Select(n => new Asset
                                          {
                                            Name = n,
                                            Type = "Unknown",
                                            Category = tmpl.Category,
                                            Collection = assetCollections.GetValueOrDefault(n) ?? "",
                                            AmountGenerated = 0
                                          })
                                          .ToList();

                     if (newAssets.Count > 0)
                     {
                       db.Assets.AddRange(newAssets);
                       await db.SaveChangesAsync();
                       existingAssets.AddRange(newAssets);
                     }

                     var assetMap = existingAssets.ToDictionary(a => a.Name, a => a.Id);
                     var revenueRecords = batchAssets.Select(kvp => new AssetRevenue
                     {
                       AssetId = assetMap[kvp.Key],
                       RevenueEntry = entry,
                       Amount = kvp.Value
                     });
                     db.AssetRevenues.AddRange(revenueRecords);
                   }
                 }
                 catch { }
               }
             }

             await db.SaveChangesAsync();
             client.Toast($"Imported {files.Count} files", "Success");
             activeMode.Set(AnalyzerMode.Home);
             filePaths.Set(new List<CurrentFile>());
             _onClose();
           }).Variant(ButtonVariant.Primary).Width(Size.Full());
    }

    // --- Main Build ---
    return activeMode.Value switch
    {
      AnalyzerMode.Home => RenderHome(),
      AnalyzerMode.Analysis => RenderHome(),
      AnalyzerMode.TemplateCreation => RenderHome(),
      AnalyzerMode.DataView => RenderHome(),
      AnalyzerMode.Preview => RenderHome(),
      AnalyzerMode.Upload => RenderHome(),
      _ => RenderHome()
    };
  }

  // --- Parsing & Analysis Helpers ---
  private List<Dictionary<string, object?>> ParseData(string filePath, ImportTemplate template)
  {
    var results = new List<Dictionary<string, object?>>();
    var headers = template.GetHeaders();
    var fileInfo = new FileInfo(filePath);
    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    using var reader = fileInfo.Extension.ToLowerInvariant() == ".csv"
        ? ExcelReaderFactory.CreateCsvReader(stream)
        : ExcelReaderFactory.CreateReader(stream);

    if (!reader.Read()) return results;

    var headerMap = new Dictionary<string, int>();
    for (int i = 0; i < reader.FieldCount; i++)
    {
      var h = reader.GetValue(i)?.ToString();
      if (h != null && headers.Contains(h)) headerMap[h] = i;
    }

    while (reader.Read())
    {
      var row = new Dictionary<string, object?>();
      foreach (var header in headers)
      {
        if (headerMap.TryGetValue(header, out int index))
          row[header] = reader.GetValue(index);
        else
          row[header] = null;
      }
      results.Add(row);
    }
    return results;
  }

  private FileAnalysis AnalyzeFile(string filePath)
  {
    var fileInfo = new FileInfo(filePath);
    var analysis = new FileAnalysis { FileName = fileInfo.Name, FileType = fileInfo.Extension.ToUpperInvariant(), FileSize = fileInfo.Length };
    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    using var reader = fileInfo.Extension.ToLowerInvariant() == ".csv" ? ExcelReaderFactory.CreateCsvReader(stream) : ExcelReaderFactory.CreateReader(stream);

    do
    {
      var sheetName = reader.Name ?? "Unknown";
      var headers = new List<string>();
      var rowCount = 0;
      var fieldCount = 0;
      if (reader.Read())
      {
        rowCount++;
        fieldCount = reader.FieldCount;
        for (int i = 0; i < reader.FieldCount; i++) headers.Add(reader.GetValue(i)?.ToString() ?? $"Column_{i}");
      }
      while (reader.Read())
      {
        rowCount++;
        if (reader.FieldCount > fieldCount) fieldCount = reader.FieldCount;
      }
      analysis.Sheets.Add(new SheetAnalysis { Name = sheetName, FieldCount = fieldCount, RowCount = rowCount, Headers = headers });
    } while (reader.NextResult());

    analysis.TotalSheets = analysis.Sheets.Count;
    return analysis;
  }

  private string FormatFileSize(long bytes)
  {
    var len = (double)bytes;
    string[] sizes = { "B", "KB", "MB", "GB" };
    int order = 0;
    while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
    return $"{len:0.##} {sizes[order]}";
  }
}
