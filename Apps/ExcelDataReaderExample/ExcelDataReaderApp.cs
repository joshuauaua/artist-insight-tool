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
    Instructions,
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

  // Dynamic Row for Data Table
  public class DynamicRow
  {
    public object? Col0 { get; set; }
    public object? Col1 { get; set; }
    public object? Col2 { get; set; }
    public object? Col3 { get; set; }
    public object? Col4 { get; set; }
    public object? Col5 { get; set; }
    public object? Col6 { get; set; }
    public object? Col7 { get; set; }
    public object? Col8 { get; set; }
    public object? Col9 { get; set; }
    public object? Col10 { get; set; }
    public object? Col11 { get; set; }
    public object? Col12 { get; set; }
    public object? Col13 { get; set; }
    public object? Col14 { get; set; }
    public object? Col15 { get; set; }
    public object? Col16 { get; set; }
    public object? Col17 { get; set; }
    public object? Col18 { get; set; }
    public object? Col19 { get; set; }
    public object? Col20 { get; set; }
    public object? Col21 { get; set; }
    public object? Col22 { get; set; }
    public object? Col23 { get; set; }
    public object? Col24 { get; set; }
    public object? Col25 { get; set; }
    public object? Col26 { get; set; }
    public object? Col27 { get; set; }
    public object? Col28 { get; set; }
    public object? Col29 { get; set; }
    public object? Col30 { get; set; }
    public object? Col31 { get; set; }
    public object? Col32 { get; set; }
    public object? Col33 { get; set; }
    public object? Col34 { get; set; }
    public object? Col35 { get; set; }
    public object? Col36 { get; set; }
    public object? Col37 { get; set; }
    public object? Col38 { get; set; }
    public object? Col39 { get; set; }
    public object? Col40 { get; set; }
    public object? Col41 { get; set; }
    public object? Col42 { get; set; }
    public object? Col43 { get; set; }
    public object? Col44 { get; set; }
    public object? Col45 { get; set; }
    public object? Col46 { get; set; }
    public object? Col47 { get; set; }
    public object? Col48 { get; set; }
    public object? Col49 { get; set; }

    public void SetValue(int index, object? value)
    {
      switch (index)
      {
        case 0: Col0 = value; break;
        case 1: Col1 = value; break;
        case 2: Col2 = value; break;
        case 3: Col3 = value; break;
        case 4: Col4 = value; break;
        case 5: Col5 = value; break;
        case 6: Col6 = value; break;
        case 7: Col7 = value; break;
        case 8: Col8 = value; break;
        case 9: Col9 = value; break;
        case 10: Col10 = value; break;
        case 11: Col11 = value; break;
        case 12: Col12 = value; break;
        case 13: Col13 = value; break;
        case 14: Col14 = value; break;
        case 15: Col15 = value; break;
        case 16: Col16 = value; break;
        case 17: Col17 = value; break;
        case 18: Col18 = value; break;
        case 19: Col19 = value; break;
        case 20: Col20 = value; break;
        case 21: Col21 = value; break;
        case 22: Col22 = value; break;
        case 23: Col23 = value; break;
        case 24: Col24 = value; break;
        case 25: Col25 = value; break;
        case 26: Col26 = value; break;
        case 27: Col27 = value; break;
        case 28: Col28 = value; break;
        case 29: Col29 = value; break;
        case 30: Col30 = value; break;
        case 31: Col31 = value; break;
        case 32: Col32 = value; break;
        case 33: Col33 = value; break;
        case 34: Col34 = value; break;
        case 35: Col35 = value; break;
        case 36: Col36 = value; break;
        case 37: Col37 = value; break;
        case 38: Col38 = value; break;
        case 39: Col39 = value; break;
        case 40: Col40 = value; break;
        case 41: Col41 = value; break;
        case 42: Col42 = value; break;
        case 43: Col43 = value; break;
        case 44: Col44 = value; break;
        case 45: Col45 = value; break;
        case 46: Col46 = value; break;
        case 47: Col47 = value; break;
        case 48: Col48 = value; break;
        case 49: Col49 = value; break;
      }
    }
  }

  public override object? Build()
  {
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();

    // --- Global State ---
    var activeMode = UseState(AnalyzerMode.Instructions);
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
          activeMode.Set(AnalyzerMode.Instructions); // Reset view on new file
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
          client.Toast($"Analyzed: {result.TotalSheets} sheets found.", "Success");

          if (isNewTemplate.Value) activeMode.Set(AnalyzerMode.TemplateCreation);
          else activeMode.Set(AnalyzerMode.Analysis);
        }
        isAnalyzing.Set(false);
      }
      else if (currentPath == null)
      {
        fileAnalysis.Set((FileAnalysis?)null);
        matchedTemplate.Set((ImportTemplate?)null);
        isNewTemplate.Set(false);
        activeMode.Set(AnalyzerMode.Instructions);
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

    object RenderLeftCard()
    {
      return new Card(
          Layout.Vertical()
          | Text.H3("Excel Analyzer")
          | Text.Muted("Upload Excel or CSV files.")
          | uploadState.ToFileInput(uploadContext).Placeholder("Select File").Width(Size.Full())
          | new Spacer()
          | (fileAnalysis.Value != null ?
              Layout.Vertical().Gap(10).Padding(10)
              | Text.Small("Status")
              | (matchedTemplate.Value != null
                  ? Layout.Vertical().Gap(5)
                      | Text.Markdown("✅ **Match Found**")
                      | Text.Small($"Template: {matchedTemplate.Value.Name}")
                      | Layout.Vertical().Gap(5)
                          | new Button("View Data Preview")
                              .Variant(activeMode.Value == AnalyzerMode.Analysis ? ButtonVariant.Primary : ButtonVariant.Outline)
                              .Icon(Icons.Info)
                              .HandleClick(() => activeMode.Set(AnalyzerMode.Analysis))
                          | new Button("View Contents")
                              .Variant(activeMode.Value == AnalyzerMode.DataView ? ButtonVariant.Primary : ButtonVariant.Outline)
                              .Icon(Icons.Eye)
                              .HandleClick(() =>
                              {
                                // Parse only if needed? Or re-parse?
                                parsedData.Set(ParseCurrentFile());
                                activeMode.Set(AnalyzerMode.DataView);
                              })
                          | new Button("Annex to Entry")
                              .Variant(activeMode.Value == AnalyzerMode.Annex ? ButtonVariant.Primary : ButtonVariant.Outline)
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
          | new Spacer()
          | (isAnalyzing.Value ? Text.Label("Processing...") : null)
      ).Width(Size.Fraction(0.3f)).Height(Size.Fit().Min(Size.Full()));
    }

    object RenderRightCard()
    {
      object content = activeMode.Value switch
      {
        AnalyzerMode.Instructions => RenderInstructions(),
        AnalyzerMode.Analysis => RenderAnalysisResults(),
        AnalyzerMode.TemplateCreation => RenderTemplateCreation(),
        AnalyzerMode.Annex => RenderAnnexForm(),
        AnalyzerMode.DataView => RenderDataTableView(),
        _ => RenderInstructions()
      };

      return new Card(content).Width(Size.Fraction(0.7f)).Height(Size.Fit().Min(Size.Full()));
    }

    object RenderInstructions()
    {
      return Layout.Vertical().Gap(20).Padding(20).Align(Align.Center)//.Justify(Justify.Center)
          | Text.H3("How to use")
          | Layout.Vertical().Gap(10)
              | Text.Markdown("1. **Upload a file** using the panel on the left.")
              | Text.Markdown("2. **Review Analysis**: If the file format is new, create a template.")
              | Text.Markdown("3. **Choose Action**: If recognized, you can View Data or Annex it to an entry.");
    }

    object RenderAnalysisResults()
    {
      if (fileAnalysis.Value == null) return RenderInstructions();

      var fa = fileAnalysis.Value;
      return Layout.Vertical().Gap(10).Padding(20)
          | Text.H3("File Analysis")
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

    object RenderTemplateCreation()
    {
      if (fileAnalysis.Value?.Sheets.Count == 0) return Text.Label("No sheets to template.");
      var headers = fileAnalysis.Value!.Sheets[0].Headers;

      return Layout.Vertical().Gap(20).Padding(20)
         .Add(Text.H3("Create Import Template"))
         .Add(Text.Muted("Define a name for this data structure."))
         .Add(Layout.Vertical().Gap(5)
             .Add(Text.Small("Detected Headers"))
             .Add(Text.Markdown($"`{string.Join(", ", headers)}`"))
         )
         .Add(Layout.Vertical().Gap(5)
             .Add("Template Name")
             .Add(newTemplateName.ToTextInput().Placeholder("e.g. Spotify Report"))
         )
         .Add(new Button("Save Template", async () =>
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
           activeMode.Set(AnalyzerMode.Analysis); // Go to analysis/options
           client.Toast("Template Created", "Success");
         }).Variant(ButtonVariant.Primary));
    }

    object RenderAnnexForm()
    {
      var options = annexEntries.Value.Select(e =>
         new Option<int?>($"{e.Description ?? "No Name"} - {e.RevenueDate:MM/dd/yyyy} - {e.Source.DescriptionText} - {e.Amount:C}", e.Id)
      ).ToList();

      return Layout.Vertical().Gap(20).Padding(20)
         .Add(Text.H3("Annex Data"))
         .Add(Text.Muted("Attach the current file content to an existing Revenue Entry."))
         .Add(Layout.Vertical().Gap(5)
             .Add("Target Entry")
             .Add(annexSelectedId.ToSelectInput(options).Placeholder("Search entry..."))
         )
         .Add(new Button("Attach Data", async () =>
         {
           if (annexSelectedId.Value == null) { client.Toast("Select an entry", "Warning"); return; }
           var data = parsedData.Value;
           if (data.Count == 0) data = ParseCurrentFile(); // Ensure data

           await using var db = factory.CreateDbContext();
           var entry = await db.RevenueEntries.FindAsync(annexSelectedId.Value);
           if (entry != null)
           {
             entry.JsonData = JsonSerializer.Serialize(data);
             await db.SaveChangesAsync();
             client.Toast($"Annexed to {entry.Description}", "Success");
             activeMode.Set(AnalyzerMode.Analysis); // Return home?
           }
         }).Variant(ButtonVariant.Primary).Disabled(annexSelectedId.Value == null));
    }

    object RenderDataTableView()
    {
      var tmpl = matchedTemplate.Value;
      var data = parsedData.Value;
      if (tmpl == null || data.Count == 0) return Text.Label("No data loaded.");

      var headers = tmpl.GetHeaders();
      // Map to DynamicRows
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

      return Layout.Vertical().Gap(10).Padding(10)
         .Add(Layout.Horizontal().Align(Align.Center).Add(Text.H3("Data View")).Add(new Spacer()).Add(Text.Muted($"{data.Count} Rows")))
         .Add(new DataTableView(dRows, Size.Full(), Size.Fit(), cols, config));
    }

    // Layout
    return Layout.Horizontal().Gap(16).Padding(20)
        .Add(RenderLeftCard())
        .Add(RenderRightCard());
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
