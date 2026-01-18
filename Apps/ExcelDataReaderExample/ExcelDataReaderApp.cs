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

[App(icon: Icons.Sheet, title: "Excel Data Reader", path: ["Pages"])]
public class ExcelDataReaderApp : ViewBase
{
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

  public override object? Build()
  {
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();

    // --- Global State ---
    var activeMode = UseState(AnalyzerMode.Home);
    var templates = UseState<List<ImportTemplate>>([]);

    // --- Analysis State ---
    var filePath = UseState<string?>(() => null);
    var fileAnalysis = UseState<FileAnalysis?>(() => null);
    var isAnalyzing = UseState(false);
    var matchedTemplate = UseState<ImportTemplate?>(() => null);
    var isNewTemplate = UseState(false);
    var parsedData = UseState<List<Dictionary<string, object?>>>([]); // Hold parsed data

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

    // --- Upload/Save State ---
    var uploadName = UseState("");
    var uploadLinkId = UseState<int?>(() => null);

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
          filePath.Set(finalPath);
          activeMode.Set(AnalyzerMode.Home); // Stay on home to show actions
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
      var currentPath = filePath.Value;
      if (currentPath != null && !isAnalyzing.Value)
      {
        isAnalyzing.Set(true);
        matchedTemplate.Set((ImportTemplate?)null);
        isNewTemplate.Set(false);

        FileAnalysis? result = null;
        try
        {
          result = await Task.Run(() =>
              {
                try { return AnalyzeFile(currentPath); }
                catch { return null; }
              });
        }
        catch { }

        if (result != null)
        {
          fileAnalysis.Set(result);
          // Match template
          if (result.Sheets.Count > 0)
          {
            var firstSheetHeaders = result.Sheets[0].Headers;
            var jsonHeaders = JsonSerializer.Serialize(firstSheetHeaders);
            var match = templates.Value.FirstOrDefault(t => t.HeadersJson == jsonHeaders);
            if (match != null) matchedTemplate.Set(match);
            else isNewTemplate.Set(true);
          }

          // Auto-redirect removed. Dialog handles next steps.
          // if (isNewTemplate.Value) activeMode.Set(AnalyzerMode.TemplateCreation); 
          // else client.Toast($"Analyzed: {result.TotalSheets} sheets found.", "Success");
        }
        isAnalyzing.Set(false);
      }
      else if (currentPath == null)
      {
        fileAnalysis.Set((FileAnalysis?)null);
        matchedTemplate.Set((ImportTemplate?)null);
        isNewTemplate.Set(false);
        activeMode.Set(AnalyzerMode.Home);
      }
    }, [filePath]);

    // Load Annex Entries only when in Annex Mode
    UseEffect(() => Task.Run(async () =>
    {
      if (activeMode.Value == AnalyzerMode.Annex && annexEntries.Value.Count == 0)
      {
        await using var db = factory.CreateDbContext();
        var results = await db.RevenueEntries
               .Include(e => e.Source)
               .OrderByDescending(e => e.RevenueDate)
               .Take(1000)
               .ToListAsync();
        annexEntries.Set(results);
      }
    }), [activeMode]);

    // --- Helpers ---
    List<Dictionary<string, object?>> ParseCurrentFile()
    {
      if (filePath.Value == null || matchedTemplate.Value == null) return [];
      return ParseData(filePath.Value, matchedTemplate.Value);
    }

    // --- Render Methods ---

    object RenderHome()
    {
      // Show dialog if analysis is ready AND (we are at Home or entered a sub-mode)
      var showDialog = fileAnalysis.Value != null &&
          (activeMode.Value == AnalyzerMode.Home ||
           activeMode.Value == AnalyzerMode.TemplateCreation ||
           activeMode.Value == AnalyzerMode.Analysis ||
           activeMode.Value == AnalyzerMode.Annex ||
           activeMode.Value == AnalyzerMode.DataView ||
           activeMode.Value == AnalyzerMode.Preview ||
           activeMode.Value == AnalyzerMode.Upload);

      string GetDialogTitle() => activeMode.Value switch
      {
        AnalyzerMode.TemplateCreation => "Create Import Template",
        AnalyzerMode.Analysis => "File Metadata",
        AnalyzerMode.Annex => "Annex Data",
        AnalyzerMode.DataView => "Data Preview",
        AnalyzerMode.Preview => "Preview File",
        AnalyzerMode.Upload => "Upload Table",
        _ => matchedTemplate.Value != null ? "Match Found" : "File Analyzed"
      };

      return new Fragment(
           Layout.Center()
           | Layout.Vertical()
               .Gap(20)
               .Padding(50)
               .Align(Align.Center)
               .Add(new Icon(Icons.Sheet).Size(48))
               .Add(Layout.Vertical().Gap(5).Align(Align.Center)
                   .Add(Text.H3("Excel Analyzer"))
                   .Add(Text.Muted("Upload analyzed financial data files."))
               )
               .Add(uploadState.ToFileInput(uploadContext).Placeholder("Select File").Width(200))
               .Add(isAnalyzing.Value ? Text.Label("Processing...") : null),

           showDialog ? new Dialog(
              _ =>
              {
                if (activeMode.Value != AnalyzerMode.Home)
                {
                  activeMode.Set(AnalyzerMode.Home);
                }
                else
                {
                  fileAnalysis.Set((FileAnalysis?)null);
                  filePath.Set((string?)null);
                  uploadState.Set((FileUpload<byte[]>?)null);
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
                   Layout.Vertical().Gap(20).Align(Align.Center)
                       | (matchedTemplate.Value != null
                          ? Layout.Vertical().Gap(15).Align(Align.Center).Width(Size.Full())
                              | Layout.Horizontal().Gap(5).Align(Align.Center)
                                  | new Icon(Icons.Check).Size(16)
                                  | Text.Small($"Matched Template: {matchedTemplate.Value.Name}")
                              | Layout.Horizontal().Gap(10).Align(Align.Center).Padding(10, 0)
                                  | new Button("Preview File", () =>
                                  {
                                    parsedData.Set(ParseCurrentFile());
                                    activeMode.Set(AnalyzerMode.Preview);
                                  }).Variant(ButtonVariant.Outline).Icon(Icons.Eye).Width(Size.Full())
                                  | new Button("Annex to Table", () =>
                                  {
                                    parsedData.Set(ParseCurrentFile());
                                    activeMode.Set(AnalyzerMode.Annex);
                                  }).Variant(ButtonVariant.Outline).Icon(Icons.Paperclip).Width(Size.Full())
                                  | new Button("Upload", () =>
                                  {
                                    uploadName.Set(fileAnalysis.Value?.FileName ?? "Untitled");
                                    parsedData.Set(ParseCurrentFile());
                                    activeMode.Set(AnalyzerMode.Upload);
                                  }).Variant(ButtonVariant.Primary).Icon(Icons.Upload).Width(Size.Full())
                          : Layout.Vertical().Gap(15).Align(Align.Center)
                              | Text.Markdown("✨ **New Structure Detected**")
                              | Text.Muted("This file structure is not recognized. Create a new template to import it.")
                              | new Button("Create Template", () => activeMode.Set(AnalyzerMode.TemplateCreation))
                                    .Variant(ButtonVariant.Primary)
                                    .Width(Size.Full())
                         )
              ),
              new DialogFooter()
            ) : null
        );
    }

    object RenderAnalysisContent()
    {
      if (fileAnalysis.Value == null) return Text.Muted("No analysis available.");
      var fa = fileAnalysis.Value;

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
      if (fileAnalysis.Value?.Sheets.Count == 0) return Text.Muted("No sheets found.");
      var headers = fileAnalysis.Value!.Sheets[0].Headers;

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
                  templateCreationStep.Set(2);
                }).Variant(ButtonVariant.Primary).Icon(Icons.ArrowRight);
      }
      else
      {
        return Layout.Vertical().Gap(10).Width(Size.Full())
            | Layout.Horizontal().Align(Align.Center).Gap(10)
                | new Button("", () => templateCreationStep.Set(1)).Variant(ButtonVariant.Ghost).Icon(Icons.ArrowLeft)
                | Text.H4("Step 2: Map Columns")
            | Text.Muted("Assign columns to standard fields.")
            | Layout.Vertical().Gap(2).Padding(10, 0)
                | headers.Select(h =>
                {
                  var currentRole = newTemplateAssetColumn.Value == h ? "Asset" :
                                    newTemplateAmountColumn.Value == h ? "Net" :
                                    newTemplateCollectionColumn.Value == h ? "Collection" :
                                    newTemplateGrossColumn.Value == h ? "Gross" :
                                    newTemplateCurrencyColumn.Value == h ? "Currency" :
                                    "Ignore";

                  return Layout.Horizontal().Gap(10).Align(Align.Center)
                      | Layout.Vertical().Width(Size.Fraction(1))
                          | Text.Label(h)
                      | Layout.Vertical().Width(150)
                          | (new DropDownMenu(
                              DropDownMenu.DefaultSelectHandler(),
                              new Button(currentRole).Variant(ButtonVariant.Outline).Icon(Icons.ChevronDown).Width(Size.Full())
                            )
                            | MenuItem.Default("Ignore").HandleSelect(() =>
                              {
                                if (newTemplateAssetColumn.Value == h) newTemplateAssetColumn.Set((string?)null);
                                if (newTemplateAmountColumn.Value == h) newTemplateAmountColumn.Set((string?)null);
                                if (newTemplateCollectionColumn.Value == h) newTemplateCollectionColumn.Set((string?)null);
                                if (newTemplateGrossColumn.Value == h) newTemplateGrossColumn.Set((string?)null);
                                if (newTemplateCurrencyColumn.Value == h) newTemplateCurrencyColumn.Set((string?)null);
                              })
                            | MenuItem.Default("Asset").HandleSelect(() =>
                              {
                                ClearColumn(h);
                                newTemplateAssetColumn.Set(h);
                              })
                            | MenuItem.Default("Collection").HandleSelect(() =>
                              {
                                ClearColumn(h);
                                newTemplateCollectionColumn.Set(h);
                              })
                            | MenuItem.Default("Gross").HandleSelect(() =>
                              {
                                ClearColumn(h);
                                newTemplateGrossColumn.Set(h);
                              })
                            | MenuItem.Default("Net").HandleSelect(() =>
                              {
                                ClearColumn(h);
                                newTemplateAmountColumn.Set(h);
                              })
                            | MenuItem.Default("Currency").HandleSelect(() =>
                              {
                                ClearColumn(h);
                                newTemplateCurrencyColumn.Set(h);
                              })
                            )
                  ;
                }).ToArray()
            | new Button("Save Template", async () =>
            {
              await using var db = factory.CreateDbContext();
              var newT = new ImportTemplate
              {
                Name = newTemplateName.Value,
                Category = newTemplateCategory.Value,
                HeadersJson = JsonSerializer.Serialize(headers),
                AssetColumn = newTemplateAssetColumn.Value,
                AmountColumn = newTemplateAmountColumn.Value,
                // Note: Collection, Gross, Currency are not yet saved to DB
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
              };
              db.ImportTemplates.Add(newT);
              await db.SaveChangesAsync();

              var fresh = await db.ImportTemplates.ToListAsync();
              templates.Set(fresh);

              var jsonHeaders = JsonSerializer.Serialize(headers);
              var match = fresh.FirstOrDefault(t => t.HeadersJson == jsonHeaders);
              if (match != null) matchedTemplate.Set(match);

              isNewTemplate.Set(false);
              activeMode.Set(AnalyzerMode.Home);
              // Reset
              templateCreationStep.Set(1);
              client.Toast("Template Created", "Success");
            }).Variant(ButtonVariant.Primary).Width(Size.Full());
      }
    }

    void ClearColumn(string h)
    {
      if (newTemplateAssetColumn.Value == h) newTemplateAssetColumn.Set((string?)null);
      if (newTemplateAmountColumn.Value == h) newTemplateAmountColumn.Set((string?)null);
      if (newTemplateCollectionColumn.Value == h) newTemplateCollectionColumn.Set((string?)null);
      if (newTemplateGrossColumn.Value == h) newTemplateGrossColumn.Set((string?)null);
      if (newTemplateCurrencyColumn.Value == h) newTemplateCurrencyColumn.Set((string?)null);
    }




    object RenderAnnexContent()
    {
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
               | Text.Muted(matchedTemplate.Value?.Name ?? "Unknown")
           | Layout.Vertical().Gap(5)
               | "Target Entry"
               | annexSelectedId.ToSelectInput(options).Placeholder("Search entry...")
           | new Button("Attach Data", async () =>
           {
             if (annexSelectedId.Value == null) { client.Toast("Select an entry", "Warning"); return; }
             var data = parsedData.Value;
             if (data.Count == 0) data = ParseCurrentFile();

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
                     // Check if it's a list of Rows (Legacy) or List of Objects (New)
                     // A simple heuristic: check first element
                     if (doc.RootElement.GetArrayLength() > 0)
                     {
                       var first = doc.RootElement[0];
                       if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("FileName", out _))
                       {
                         // It is ALREADY a list of sheets
                         existingSheets = JsonSerializer.Deserialize<List<object>>(entry.JsonData) ?? [];
                       }
                       else
                       {
                         // It is a list of rows (Legacy)
                         var rows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(entry.JsonData);
                         existingSheets.Add(new { Title = "Legacy Data", FileName = "Legacy", TemplateName = "Unknown", Rows = rows });
                       }
                     }
                   }
                   else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                   {
                     // Single Sheet Object (Previous Format)
                     var obj = JsonSerializer.Deserialize<object>(entry.JsonData);
                     if (obj != null) existingSheets.Add(obj);
                   }
                 }
                 catch { /* Ignore corrupt data */ }
               }

               // Append new sheet
               var payload = new
               {
                 Title = annexTitle.Value,
                 FileName = fileAnalysis.Value?.FileName ?? "Unknown File",
                 TemplateName = matchedTemplate.Value?.Name ?? "Unknown Template",
                 Rows = data
               };
               existingSheets.Add(payload);

               entry.JsonData = JsonSerializer.Serialize(existingSheets);

               // --- Asset Extraction Logic ---
               var tmpl = matchedTemplate.Value;
               if (tmpl != null && !string.IsNullOrEmpty(tmpl.AssetColumn) && !string.IsNullOrEmpty(tmpl.AmountColumn))
               {
                 try
                 {
                   var assetCol = tmpl.AssetColumn;
                   var amountCol = tmpl.AmountColumn;

                   // 1. Identify Assets in this batch
                   var batchAssets = new Dictionary<string, decimal>();
                   foreach (var row in data)
                   {
                     if (row.TryGetValue(assetCol, out var nameObj) && row.TryGetValue(amountCol, out var amountObj))
                     {
                       var name = nameObj?.ToString()?.Trim();
                       if (!string.IsNullOrEmpty(name) && decimal.TryParse(amountObj?.ToString(), out var amount))
                       {
                         if (!batchAssets.ContainsKey(name)) batchAssets[name] = 0;
                         batchAssets[name] += amount;
                       }
                     }
                   }

                   if (batchAssets.Count > 0)
                   {
                     // 2. Find existing Assets
                     var names = batchAssets.Keys.ToList();
                     var existingAssets = await db.Assets.Where(a => names.Contains(a.Name)).ToListAsync();
                     var existingNames = existingAssets.Select(a => a.Name).ToHashSet();

                     // 3. Create missing Assets
                     var newAssets = names.Where(n => !existingNames.Contains(n))
                                          .Select(n => new Asset { Name = n, Type = "Unknown", AmountGenerated = 0 })
                                          .ToList();

                     if (newAssets.Count > 0)
                     {
                       db.Assets.AddRange(newAssets);
                       await db.SaveChangesAsync();
                       existingAssets.AddRange(newAssets);
                     }

                     // 4. Create Revenue Records
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
               annexTitle.Set(""); // Reset
             }
           }).Variant(ButtonVariant.Primary).Disabled(annexSelectedId.Value == null);
    }


    object RenderDataTableView(bool isEmbedded = false)
    {
      var tmpl = matchedTemplate.Value;
      var data = parsedData.Value;
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
      return Layout.Vertical().Gap(20).Width(Size.Full())
          | Text.Label("File Metadata")
          | RenderAnalysisContent()
          | Text.Label("Data Preview")
          | RenderDataTableView(isEmbedded: true);
    }

    object RenderUploadContent()
    {
      var options = annexEntries.Value.Select(e =>
         new Option<int?>($"{e.Description ?? "No Name"} - {e.RevenueDate:MM/dd/yyyy}", e.Id)
      ).ToList();
      options.Insert(0, new Option<int?>("Create New Entry (Default)", null));

      return Layout.Vertical().Gap(15).Width(Size.Full())
          | Text.Muted("Upload this table to Data Tables.")
          | Layout.Vertical().Gap(5)
              | Text.Label("Name")
              | uploadName.ToTextInput().Placeholder("Table Name")
          | Layout.Vertical().Gap(5)
              | Text.Label("Template Used")
              | Text.Muted(matchedTemplate.Value?.Name ?? "Unknown")
          | Layout.Vertical().Gap(5)
              | Text.Label("Link to Entry (Optional)")
              | uploadLinkId.ToSelectInput(options)

          | new Button("Upload Table", async () =>
          {
            if (string.IsNullOrWhiteSpace(uploadName.Value)) { client.Toast("Name required", "Warning"); return; }
            var data = parsedData.Value;
            if (data.Count == 0) data = ParseCurrentFile();

            await using var db = factory.CreateDbContext();
            RevenueEntry? targetEntry = null;

            if (uploadLinkId.Value != null)
            {
              // Append to existing
              targetEntry = await db.RevenueEntries.FindAsync(uploadLinkId.Value);
              if (targetEntry == null) { client.Toast("Entry not found", "Error"); return; }
            }
            else
            {
              // Create New
              var otherSource = await db.RevenueSources.FirstOrDefaultAsync(s => s.DescriptionText == "Other");
              targetEntry = new RevenueEntry
              {
                Description = uploadName.Value,
                RevenueDate = DateTime.Now,
                Amount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SourceId = otherSource?.Id ?? 1,
                ArtistId = 1, // Default Artist
                ImportTemplateId = matchedTemplate.Value?.Id
              };
              db.RevenueEntries.Add(targetEntry);
            }

            // Logic to append data
            var existingSheets = new List<object>();
            if (!string.IsNullOrEmpty(targetEntry.JsonData))
            {
              try
              {
                using var doc = JsonDocument.Parse(targetEntry.JsonData);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                  if (doc.RootElement.GetArrayLength() > 0)
                  {
                    var first = doc.RootElement[0];
                    if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("FileName", out _))
                    {
                      existingSheets = JsonSerializer.Deserialize<List<object>>(targetEntry.JsonData) ?? [];
                    }
                    else
                    {
                      var rows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(targetEntry.JsonData);
                      existingSheets.Add(new { Title = "Legacy Data", FileName = "Legacy", TemplateName = "Unknown", Rows = rows });
                    }
                  }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                  var obj = JsonSerializer.Deserialize<object>(targetEntry.JsonData);
                  if (obj != null) existingSheets.Add(obj);
                }
              }
              catch { }
            }

            var payload = new
            {
              Title = uploadName.Value,
              FileName = fileAnalysis.Value?.FileName ?? "Unknown File",
              TemplateName = matchedTemplate.Value?.Name ?? "Unknown Template",
              Rows = data
            };
            existingSheets.Add(payload);
            targetEntry.JsonData = JsonSerializer.Serialize(existingSheets);
            targetEntry.UpdatedAt = DateTime.UtcNow;

            // --- Asset Extraction ---
            var tmpl = matchedTemplate.Value;
            if (tmpl != null && !string.IsNullOrEmpty(tmpl.AssetColumn) && !string.IsNullOrEmpty(tmpl.AmountColumn))
            {
              try
              {
                var assetCol = tmpl.AssetColumn;
                var amountCol = tmpl.AmountColumn;
                var batchAssets = new Dictionary<string, decimal>();
                foreach (var row in data)
                {
                  if (row.TryGetValue(assetCol, out var nameObj) && row.TryGetValue(amountCol, out var amountObj))
                  {
                    var name = nameObj?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(name) && decimal.TryParse(amountObj?.ToString(), out var amount))
                    {
                      if (!batchAssets.ContainsKey(name)) batchAssets[name] = 0;
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
                                         .Select(n => new Asset { Name = n, Type = "Unknown", AmountGenerated = 0 })
                                         .ToList();
                  if (newAssets.Count > 0)
                  {
                    db.Assets.AddRange(newAssets);
                    await db.SaveChangesAsync();
                    existingAssets.AddRange(newAssets);
                  }
                  // 4. Create Revenue Records
                  var assetMap = existingAssets.ToDictionary(a => a.Name, a => a.Id);
                  var revenueRecords = batchAssets.Select(kvp => new AssetRevenue
                  {
                    AssetId = assetMap[kvp.Key],
                    RevenueEntry = targetEntry,
                    Amount = kvp.Value
                  });
                  db.AssetRevenues.AddRange(revenueRecords);
                }
              }
              catch { }
            }

            await db.SaveChangesAsync();
            client.Toast("Uploaded Successfully", "Success");
            activeMode.Set(AnalyzerMode.Home);
            fileAnalysis.Set((FileAnalysis?)null);
            filePath.Set((string?)null);
            uploadState.Set((FileUpload<byte[]>?)null);

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
