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

[App(icon: Icons.Sheet, title: "ExcelDataReader", path: ["Examples", "ExcelDataReader"])]
public class ExcelDataReaderApp : ViewBase
{
  private enum AnalyzerMode
  {
    Home,
    Analysis,
    TemplateCreation,
    Annex,
    DataView
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

    // --- Template Creation State ---
    var newTemplateName = UseState("");

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

          if (isNewTemplate.Value) activeMode.Set(AnalyzerMode.TemplateCreation);
          else client.Toast($"Analyzed: {result.TotalSheets} sheets found.", "Success");
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
      return Layout.Center()
          | new Card(
              Layout.Vertical().Gap(15).Align(Align.Center)
              | Text.H3("Excel Analyzer")
              | Text.Muted("Upload analyzed financial data files.")
              | uploadState.ToFileInput(uploadContext).Placeholder("Select File").Width(Size.Full())
              | new Spacer()
              | (fileAnalysis.Value != null ?
                  Layout.Vertical().Gap(10).Padding(10)
                  | Text.Small("Status")
                  | (matchedTemplate.Value != null
                      ? Layout.Vertical().Gap(5)
                          | Text.Markdown("✅ **Match Found**")
                          | Text.Small($"Template: {matchedTemplate.Value.Name}")
                          | Layout.Vertical().Gap(10).Padding(10, 0, 0, 0)
                              | new Button("View Analysis Results")
                                  .Variant(ButtonVariant.Outline)
                                  .Icon(Icons.Info)
                                  .HandleClick(() => activeMode.Set(AnalyzerMode.Analysis))
                              | new Button("View Contents")
                                  .Variant(ButtonVariant.Primary)
                                  .Icon(Icons.Eye)
                                  .HandleClick(() =>
                                  {
                                    parsedData.Set(ParseCurrentFile());
                                    activeMode.Set(AnalyzerMode.DataView);
                                  })
                              | new Button("Annex to Entry")
                                  .Variant(ButtonVariant.Secondary)
                                  .Icon(Icons.Link)
                                  .HandleClick(() =>
                                  {
                                    parsedData.Set(ParseCurrentFile());
                                    activeMode.Set(AnalyzerMode.Annex);
                                  })
                      : (isNewTemplate.Value
                          ? Layout.Vertical().Gap(5)
                              | Text.Markdown("✨ **New Structure**")
                              | new Button("Create Template", () => activeMode.Set(AnalyzerMode.TemplateCreation)).Variant(ButtonVariant.Primary)
                          : Text.Small("Analyzing...")))
                  : null)
              | (isAnalyzing.Value ? Text.Label("Processing...") : null)
          ).Width(500);
    }

    object RenderAnalysisResults()
    {
      if (fileAnalysis.Value == null) return RenderHome();
      var fa = fileAnalysis.Value;

      return Layout.Vertical().Gap(16).Padding(20)
          | Layout.Horizontal().Gap(10).Align(Align.Center)
              | new Button("Back", () => activeMode.Set(AnalyzerMode.Home)).Variant(ButtonVariant.Ghost).Icon(Icons.ArrowLeft)
              | Text.H3("File Analysis")
          | new Card(
              Layout.Vertical().Gap(10)
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
              )
          );
    }

    object RenderTemplateCreation()
    {
      if (fileAnalysis.Value?.Sheets.Count == 0) return RenderHome();
      var headers = fileAnalysis.Value!.Sheets[0].Headers;

      return Layout.Center()
         | new Card(
             Layout.Vertical().Gap(20).Padding(20)
             | Layout.Horizontal().Gap(10).Align(Align.Center)
                 | new Button("Back", () => activeMode.Set(AnalyzerMode.Home)).Variant(ButtonVariant.Ghost).Icon(Icons.ArrowLeft)
                 | Text.H3("Create Import Template")
             | Text.Muted("Define a name for this data structure.")
             | Layout.Vertical().Gap(5)
                 | Text.Small("Detected Headers")
                 | Text.Markdown($"`{string.Join(", ", headers)}`")
             | Layout.Vertical().Gap(5)
                 | "Template Name"
                 | newTemplateName.ToTextInput().Placeholder("e.g. Spotify Report")
             | new Button("Save Template", async () =>
             {
               if (string.IsNullOrWhiteSpace(newTemplateName.Value)) { client.Toast("Name required", "Warning"); return; }

               await using var db = factory.CreateDbContext();
               var newT = new ImportTemplate
               {
                 Name = newTemplateName.Value,
                 HeadersJson = JsonSerializer.Serialize(headers),
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
               client.Toast("Template Created", "Success");
             }).Variant(ButtonVariant.Primary)
         ).Width(600);
    }

    object RenderAnnexForm()
    {
      var options = annexEntries.Value.Select(e =>
         new Option<int?>($"{e.Description ?? "No Name"} - {e.RevenueDate:MM/dd/yyyy} - {e.Source.DescriptionText} - {e.Amount:C}", e.Id)
      ).ToList();

      return Layout.Center()
         | new Card(
             Layout.Vertical().Gap(20).Padding(20)
             | Layout.Horizontal().Gap(10).Align(Align.Center)
                 | new Button("Back", () => activeMode.Set(AnalyzerMode.Home)).Variant(ButtonVariant.Ghost).Icon(Icons.ArrowLeft)
                 | Text.H3("Annex Data")
             | Text.Muted("Attach the current file content to an existing Revenue Entry.")
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
                 entry.JsonData = JsonSerializer.Serialize(data);
                 await db.SaveChangesAsync();
                 client.Toast($"Annexed to {entry.Description}", "Success");
                 activeMode.Set(AnalyzerMode.Home);
               }
             }).Variant(ButtonVariant.Primary).Disabled(annexSelectedId.Value == null)
         ).Width(600);
    }

    object RenderDataTableView()
    {
      var tmpl = matchedTemplate.Value;
      var data = parsedData.Value;
      if (tmpl == null || data.Count == 0) return RenderHome();

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

      return Layout.Vertical().Gap(10).Padding(20)
         | new Card(
             Layout.Vertical().Gap(10)
             | Layout.Horizontal().Gap(10).Align(Align.Center)
                 | new Button("Back", () => activeMode.Set(AnalyzerMode.Home)).Variant(ButtonVariant.Outline).Icon(Icons.ArrowLeft)
                 | Text.H3("Data View")
                 | new Spacer()
                 | Text.Muted($"{data.Count} Rows")
             | new DataTableView(dRows, Size.Full(), Size.Fit(), cols, config)
         );
    }

    // --- Main Build ---
    return activeMode.Value switch
    {
      AnalyzerMode.Home => RenderHome(),
      AnalyzerMode.Analysis => RenderAnalysisResults(),
      AnalyzerMode.TemplateCreation => RenderTemplateCreation(),
      AnalyzerMode.Annex => RenderAnnexForm(),
      AnalyzerMode.DataView => RenderDataTableView(),
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
