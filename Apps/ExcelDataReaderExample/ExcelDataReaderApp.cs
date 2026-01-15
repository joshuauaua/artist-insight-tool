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
  // Tuple to hold state for the data view: Data + Template
  private record DataViewContext(List<Dictionary<string, object?>> Data, ImportTemplate Template);

  public override object? Build()
  {
    // State to control which view is active
    var activeViewData = UseState<DataViewContext?>(() => null);

    if (activeViewData.Value != null)
    {
      return new DataPageView(
          activeViewData.Value.Data,
          activeViewData.Value.Template,
          onBack: () => activeViewData.Set((DataViewContext?)null)
      );
    }

    return new AnalyzerView(
        onViewData: (data, template) => activeViewData.Set(new DataViewContext(data, template))
    );
  }
}

public class AnalyzerView(Action<List<Dictionary<string, object?>>, ImportTemplate> onViewData) : ViewBase
{
  // Model for storing file analysis
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
    // Ensure encodings support
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();

    var filePath = UseState<string?>(() => null);
    var fileAnalysis = UseState<FileAnalysis?>(() => null);
    var isAnalyzing = UseState(false);

    // Template Logic States
    var templates = UseState<List<ImportTemplate>>([]);
    var matchedTemplate = UseState<ImportTemplate?>(() => null);
    var isNewTemplate = UseState(false);
    var showSaveSheet = UseState(false);

    // Upload state for files
    var uploadState = UseState<FileUpload<byte[]>?>();
    var uploadContext = this.UseUpload(MemoryStreamUploadHandler.Create(uploadState))
        .Accept(".xlsx,.xls,.csv")
        .MaxFileSize(50 * 1024 * 1024);

    var fileName = uploadState.Value?.FileName;

    // Load templates
    UseEffect(async () =>
    {
      try
      {
        await using var db = factory.CreateDbContext();
        var loaded = await db.ImportTemplates.ToListAsync();
        templates.Set(loaded);
      }
      catch (Exception) { /* If disposed, ignore */ }
    }, []);

    // When a file is uploaded, save it to temp file
    UseEffect(() =>
    {
      if (uploadState.Value?.Content is byte[] bytes && bytes.Length > 0)
      {
        try
        {
          var tempPath = System.IO.Path.GetTempFileName();
          var extension = ".xlsx"; // Default

          // Simple extension detection
          if (bytes.Length >= 4)
          {
            if (bytes[0] == 0x50 && bytes[1] == 0x4B)
            {
              extension = ".xlsx";
            }
            else if (bytes[0] == 0xD0 && bytes[1] == 0xCF)
            {
              extension = ".xls";
            }
            else
            {
              var content = System.Text.Encoding.UTF8.GetString(bytes.Take(100).ToArray());
              if (content.Contains(','))
              {
                extension = ".csv";
              }
            }
          }

          var finalPath = tempPath + extension;
          File.WriteAllBytes(finalPath, bytes);
          filePath.Set(finalPath);
        }
        catch (Exception ex)
        {
          try { client.Toast($"File upload error: {ex.Message}", "Error"); } catch { }
        }
      }
    }, [uploadState]);

    // Analyze file and check templates
    UseEffect(async () =>
    {
      var currentPath = filePath.Value;
      if (currentPath != null && !isAnalyzing.Value)
      {
        isAnalyzing.Set(true);
        matchedTemplate.Set((ImportTemplate?)null);
        isNewTemplate.Set(false);

        // Analyze in background
        FileAnalysis? result = null;
        try
        {
          result = await Task.Run(() =>
              {
                try { return AnalyzeFile(currentPath); }
                catch (Exception ex)
                {
                  Console.WriteLine($"Analysis Error: {ex.Message}");
                  return null;
                }
              });
        }
        catch { /* Ignore */ }

        if (result != null)
        {
          try
          {
            fileAnalysis.Set(result);

            if (result.Sheets.Count > 0)
            {
              var firstSheetHeaders = result.Sheets[0].Headers;
              var jsonHeaders = JsonSerializer.Serialize(firstSheetHeaders);

              var match = templates.Value.FirstOrDefault(t => t.HeadersJson == jsonHeaders);
              if (match != null) matchedTemplate.Set(match);
              else isNewTemplate.Set(true);
            }
            client.Toast($"Analyzed: {result.TotalSheets} sheets found.", "Success");
          }
          catch (Exception) { /* Ignore disposal errors */ }
        }

        try { isAnalyzing.Set(false); } catch { }
      }
      else if (currentPath == null)
      {
        // Reset
        try
        {
          fileAnalysis.Set((FileAnalysis?)null);
          matchedTemplate.Set((ImportTemplate?)null);
          isNewTemplate.Set(false);
        }
        catch { }
      }
    }, [filePath]);

    // Handle Sheet
    if (showSaveSheet.Value && fileAnalysis.Value?.Sheets.Count > 0)
    {
      var headers = fileAnalysis.Value.Sheets[0].Headers;
      return new SaveTemplateSheet(headers, () =>
      {
        showSaveSheet.Set(false);
        // Reload templates & recheck
        Task.Run(async () =>
        {
          await using var db = factory.CreateDbContext();
          var freshTemplates = await db.ImportTemplates.ToListAsync();
          templates.Set(freshTemplates);

          var jsonHeaders = JsonSerializer.Serialize(headers);
          var match = freshTemplates.FirstOrDefault(t => t.HeadersJson == jsonHeaders);
          if (match != null)
          {
            matchedTemplate.Set(match);
            isNewTemplate.Set(false);
          }
        });
      }, () => showSaveSheet.Set(false));
    }

    return Layout.Horizontal().Gap(16).Padding(20) // Main container padding
        | new Card(
            Layout.Vertical()
            | Text.H3("Excel Analyzer")
            | Text.Muted("Upload Excel (.xlsx, .xls) or CSV files.")
            | uploadState.ToFileInput(uploadContext)
                .Placeholder("Select Excel/CSV file")
                .Width(Size.Full())
            | new Spacer()
            | (fileAnalysis.Value != null
                ? Layout.Vertical().Gap(10).Padding(10)
                    | Text.Small("Template Status")
                    | (matchedTemplate.Value != null
                        ? Layout.Vertical().Gap(5)
                            | Text.Markdown("✅ **Match Found**")
                            | Text.Small($"Template: {matchedTemplate.Value.Name}")
                            | new Button("View Data")
                                .Primary()
                                .HandleClick(() =>
                                {
                                  var data = ParseData(filePath.Value!, matchedTemplate.Value);
                                  onViewData(data, matchedTemplate.Value);
                                })
                        : (isNewTemplate.Value
                            ? Layout.Vertical().Gap(5)
                                | Text.Markdown("✨ **New Data Structure**")
                                | Text.Small("This structure has not been seen before.")
                                | new Button("Save as Template", () => showSaveSheet.Set(true)).Variant(ButtonVariant.Primary)
                            : Text.Small("Analyzing...")))
                : null)
            | new Spacer()
            | (isAnalyzing.Value ? Text.Label("Analyzing file...") : null)
        ).Width(Size.Fraction(0.4f)).Height(Size.Fit().Min(Size.Full()))

        | new Card(
            fileAnalysis.Value != null
            ? Layout.Vertical()
                | Text.H3("File Analysis Results")
                 | new Markdown($"""                                
                            | Property | Value |
                            |----------|-------|
                            | **File name** | `{fileName}` |
                            | **Type** | `{fileAnalysis.Value.FileType}` |
                            | **Size** | `{FormatFileSize(fileAnalysis.Value.FileSize)}` |
                            """)
                | Layout.Vertical(
                    fileAnalysis.Value.Sheets.Select((sheet, index) =>
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
            : Layout.Center() | Text.Muted("Upload a file to see results")
        ).Width(Size.Fraction(0.6f)).Height(Size.Fit().Min(Size.Full()));
  }

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
    var analysis = new FileAnalysis
    {
      FileName = fileInfo.Name,
      FileType = fileInfo.Extension.ToUpperInvariant(),
      FileSize = fileInfo.Length
    };

    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    using var reader = fileInfo.Extension.ToLowerInvariant() == ".csv"
        ? ExcelReaderFactory.CreateCsvReader(stream)
        : ExcelReaderFactory.CreateReader(stream);

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
        for (int i = 0; i < reader.FieldCount; i++)
        {
          var header = reader.GetValue(i)?.ToString() ?? $"Column_{i}";
          headers.Add(header);
        }
      }

      while (reader.Read())
      {
        rowCount++;
        if (reader.FieldCount > fieldCount) fieldCount = reader.FieldCount;
      }

      analysis.Sheets.Add(new SheetAnalysis
      {
        Name = sheetName,
        FieldCount = fieldCount,
        RowCount = rowCount,
        Headers = headers
      });

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

public class DataPageView(List<Dictionary<string, object?>> data, ImportTemplate template, Action onBack) : ViewBase
{
  public override object? Build()
  {
    var headers = template.GetHeaders();

    // Map dictionaries to DynamicRow objects
    // We map the first column in the dictionary to Col0, second to Col1, etc.
    // This allows reasonable support for up to 50 columns.
    var dynamicRows = data.Select(dict =>
    {
      var row = new DynamicRow();
      int i = 0;
      foreach (var header in headers)
      {
        // Safeguard: don't exceed 50 columns
        if (i >= 50) break;

        var val = dict.ContainsKey(header) ? dict[header] : null;

        // Reflection set is slow but done once per row on load. 
        // For better perf we could use a switch or generated code, but reflection is fine for < 10k rows usually.
        // Actually, direct assignment is cleaner if we had a mapped method, but simple reflection set is easiest for now without massive switch.
        // Wait, I can just use a helper method inside DynamicRow to set by index? 
        // No, let's just use reflection to set Col{i} or a massive switch in DynamicRow.
        // A SetValue(index, val) method in DynamicRow with a switch case is best for perf.
        row.SetValue(i, val);
        i++;
      }
      return row;
    }).AsQueryable();

    // Create Columns 
    // Name = "Col0", "Col1" ...
    var columns = headers.Select((header, index) =>
    {
      if (index >= 50) return null; // Skip if > 50

      return new DataTableColumn
      {
        Name = $"Col{index}",   // Property on DynamicRow
        Header = header,        // Display Name
        Order = index,
        ColType = ColType.Text,
        Align = Align.Left,
        Sortable = true,
        Filterable = true
      };
    }).Where(c => c != null).Cast<DataTableColumn>().ToArray();

    // Configure DataTable
    var config = new DataTableConfig
    {
      FreezeColumns = 1,
      ShowGroups = true,
      ShowIndexColumn = true,
      SelectionMode = SelectionModes.Rows,
      AllowCopySelection = true,
      AllowColumnReordering = true,
      AllowColumnResizing = true,
      AllowLlmFiltering = false, // Disable LLM filtering for now as it might assume semantic names
      AllowSorting = true,
      AllowFiltering = true,
      ShowSearch = true,
      EnableCellClickEvents = true,
      ShowVerticalBorders = true
    };

    return Layout.Vertical().Padding(20).Gap(20)//.Height(Size.Full()) // Allow page scrolling
                                                // Header Card
        | new Card(
            Layout.Horizontal().Gap(20).Align(Align.Center)
                | new Button("Back to Analyzer", onBack).Icon(Icons.ArrowLeft).Variant(ButtonVariant.Outline)
                | Text.H3(template.Name)
                | Text.Muted($"{data.Count} Records")
                | new Spacer()
        )

        // Data Table Card
        | new Card(
             new DataTableView(
                queryable: dynamicRows,
                width: Size.Full(),
                height: Size.Fit(), // Allow table to grow with content, page will scroll
                columns: columns,
                config: config
            )
        );//.Height(Size.Grow()); // Remove strict grow constraint
  }
}

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

public class SaveTemplateSheet(List<string> headers, Action onSave, Action onCancel) : ViewBase
{
  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();

    var nameState = UseState("");

    return Layout.Vertical().Gap(20).Padding(20).Width(400)
        .Add(Text.H3("Save Import Template"))
        .Add(Text.Muted("This data structure is new. Give it a name to recognize it in the future."))

        .Add(Layout.Vertical().Gap(5)
            .Add(Text.Small("Headers Detected"))
            .Add(Layout.Vertical().Padding(10)
                .Add(Text.Small(string.Join(", ", headers)))
            )
        )

        .Add(Layout.Vertical().Gap(5)
            .Add(Text.Small("Template Name"))
            .Add(nameState.ToTextInput().Placeholder("e.g. Spotify Distribution Report"))
        )

        .Add(Layout.Horizontal().Gap(10).Align(Align.Right)
            .Add(new Button("Cancel", onCancel).Variant(ButtonVariant.Outline))
            .Add(new Button("Save Template", async () =>
            {
              if (string.IsNullOrWhiteSpace(nameState.Value))
              {
                client.Toast("Please enter a name", "Warning");
                return;
              }

              await using var db = factory.CreateDbContext();
              var newTemplate = new ImportTemplate
              {
                Name = nameState.Value,
                HeadersJson = JsonSerializer.Serialize(headers),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
              };

              db.ImportTemplates.Add(newTemplate);
              await db.SaveChangesAsync();

              client.Toast("Template saved successfully!", "Success");
              onSave();
            }).Variant(ButtonVariant.Primary))
        );
  }
}
