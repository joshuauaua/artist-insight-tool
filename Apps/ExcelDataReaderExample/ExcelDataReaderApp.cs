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
    var searchQuery = UseState("");

    // Filter data
    var filteredData = data;
    if (!string.IsNullOrWhiteSpace(searchQuery.Value))
    {
      var q = searchQuery.Value.ToLowerInvariant();
      filteredData = data.Where(row =>
          row.Values.Any(v => v?.ToString()?.ToLowerInvariant().Contains(q) == true)
      ).ToList();
    }

    // Limit display for performance 
    var displayData = filteredData.Take(100).ToList();

    // Generate Markdown Table
    var sb = new System.Text.StringBuilder();

    // Header
    sb.Append("| ");
    sb.Append(string.Join(" | ", headers));
    sb.AppendLine(" |");

    // Separator
    sb.Append("| ");
    sb.Append(string.Join(" | ", headers.Select(_ => "---")));
    sb.AppendLine(" |");

    // Rows
    foreach (var row in displayData)
    {
      sb.Append("| ");
      // Careful with null values in dictionary
      var values = headers.Select(h =>
      {
        var val = row.ContainsKey(h) ? row[h]?.ToString() ?? "" : "";
        // Escape pipes in content to prevent breaking table
        return val.Replace("|", "\\|").Replace("\n", " ");
      });
      sb.Append(string.Join(" | ", values));
      sb.AppendLine(" |");
    }

    return Layout.Vertical().Padding(20).Gap(20).Height(Size.Full())
        // Top Bar
        | Layout.Horizontal().Gap(20).Align(Align.Center)
            | new Button("Back to Analyzer", onBack).Icon(Icons.ArrowLeft).Variant(ButtonVariant.Outline)
            | Text.H3(template.Name)
            | Text.Muted($"{filteredData.Count} Records")
            | new Spacer()
            | searchQuery.ToTextInput().Placeholder("Search data...").Width(300)

        // Table - remove invalid .Scrollable() and .Padding() on Markdown
        | Layout.Vertical().Padding(0).Height(Size.Full())
            | Layout.Vertical().Padding(10).Add(new Markdown(sb.ToString()))
            | (filteredData.Count > 100
                ? Layout.Center().Padding(20).Add(Text.Muted($"Showing 100 of {filteredData.Count} records. Refine search to see more."))
                : null
              );
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
