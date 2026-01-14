using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using ExcelDataReader;
using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;

namespace ExcelDataReaderExample;

[App(icon: Icons.Sheet, title: "ExcelDataReader", path: ["Examples", "ExcelDataReader"])]
public class ExcelDataReaderApp : ViewBase
{
  // Model for storing file analysis
  public record FileAnalysis
  {
    public string FileName { get; set; } = "";
    public string FileType { get; set; } = "";
    public long FileSize { get; set; }
    public int TotalSheets { get; set; }
    public List<SheetAnalysis> Sheets { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
  }

  public record SheetAnalysis
  {
    public string Name { get; set; } = "";
    public string CodeName { get; set; } = "";
    public int FieldCount { get; set; }
    public int RowCount { get; set; }
    public int MergeCellsCount { get; set; }
    public List<string> Headers { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
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
      await using var db = factory.CreateDbContext();
      templates.Set(await db.ImportTemplates.ToListAsync());
      return (IDisposable?)null;
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
          client.Toast($"File upload error: {ex.Message}", "Error");
        }
      }
    }, [uploadState]);

    // Clear analysis and filePath when file is removed
    UseEffect(() =>
    {
      if (uploadState.Value == null && filePath.Value != null)
      {
        filePath.Set((string?)null);
      }
    }, uploadState);

    // Analyze file and check templates
    UseEffect(() =>
    {
      if (filePath.Value != null && !isAnalyzing.Value)
      {
        isAnalyzing.Set(true);
        matchedTemplate.Set((ImportTemplate?)null);
        isNewTemplate.Set(false);

        Task.Run(() =>
            {
              try
              {
                var result = AnalyzeFile(filePath.Value);
                fileAnalysis.Set(result);

                // Template Matching Logic (Based on first sheet)
                if (result.Sheets.Count > 0)
                {
                  var firstSheetHeaders = result.Sheets[0].Headers;
                  var jsonHeaders = JsonSerializer.Serialize(firstSheetHeaders);

                  // Find match
                  var match = templates.Value.FirstOrDefault(t => t.HeadersJson == jsonHeaders);
                  if (match != null)
                  {
                    matchedTemplate.Set(match);
                  }
                  else
                  {
                    // Simple check: Check if headers match any known set regardless of order? 
                    // For now, assume exact order matters as per user's "headers identified".
                    // Or maybe set comparison?
                    // Let's do exact match first.
                    isNewTemplate.Set(true);
                  }
                }

                client.Toast($"File analyzed successfully! Found {result.TotalSheets} sheets.", "Success");
              }
              catch (Exception ex)
              {
                client.Toast($"Analysis error: {ex.Message}", "Error");
                Console.WriteLine($"Analysis Error: {ex.Message}");
              }
              finally
              {
                isAnalyzing.Set(false);
              }
            });
      }
      else if (filePath.Value == null)
      {
        fileAnalysis.Set((FileAnalysis?)null);
        matchedTemplate.Set((ImportTemplate?)null);
        isNewTemplate.Set(false);
      }
    }, [filePath]); // removed templates from dep to avoid re-analysis on save

    // Handle Sheet
    if (showSaveSheet.Value && fileAnalysis.Value?.Sheets.Count > 0)
    {
      var headers = fileAnalysis.Value.Sheets[0].Headers;
      return new SaveTemplateSheet(headers, () =>
      {
        showSaveSheet.Set(false);
        // Reload templates
        Task.Run(async () =>
        {
          await using var db = factory.CreateDbContext();
          var freshTemplates = await db.ImportTemplates.ToListAsync();
          templates.Set(freshTemplates);

          // Re-check current file?
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

    return Layout.Horizontal(
        // Left Card - Functionality
        new Card(
            Layout.Vertical(
                Text.H3("Excel File Analyzer"),
                Text.Muted("Upload Excel (.xlsx, .xls) or CSV files."),
                uploadState.ToFileInput(uploadContext)
                    .Placeholder("Select Excel/CSV file")
                    .Width(Size.Full()),

                new Spacer(),

                // Template Status Section
                fileAnalysis.Value != null ?
                    Layout.Vertical().Gap(10).Padding(10)
                    .Add(Text.Small("Template Status"))
                    .Add(
                        matchedTemplate.Value != null
                         ? Layout.Vertical().Gap(5)
                            .Add(Text.Markdown("✅ **Match Found**"))
                            .Add(Text.Small($"Template: {matchedTemplate.Value.Name}"))
                         : (isNewTemplate.Value
                            ? Layout.Vertical().Gap(5)
                                .Add(Text.Markdown("✨ **New Data Structure**"))
                                .Add(Text.Small("This structure has not been seen before."))
                                .Add(new Button("Save as Template", () => showSaveSheet.Set(true)).Variant(ButtonVariant.Primary))
                            : Text.Small("Analyzing..."))
                    )
                    : null,


                new Spacer(),
                Text.Small("Uses ExcelDataReader package."),

                isAnalyzing.Value ?
                    Layout.Horizontal(
                        Text.Label("Analyzing file...")
                    ).Align(Align.Left) : null
            )
        ).Width(Size.Fraction(0.4f)).Height(Size.Fit().Min(Size.Full())),

        // Right Card - Analysis Results
        new Card(
            fileAnalysis.Value != null ? (
                Layout.Vertical(
                    Text.H3("File Analysis Results"),
                    // ... stats table ...
                    Layout.Vertical(
                        new Markdown($"""                                
                                | Property | Value |
                                |----------|-------|
                                | **File name** | `{fileName}` |
                                | **Type** | `{fileAnalysis.Value.FileType}` |
                                | **Size** | `{FormatFileSize(fileAnalysis.Value.FileSize)}` |
                                | **Format** | `{(matchedTemplate.Value != null ? matchedTemplate.Value.Name : "Unknown")}` |
                                """)
                    ),

                    // Sheet list
                    Layout.Vertical(
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
                )
            ) : Layout.Center().Add(Text.Muted("Upload a file to see results"))
        ).Width(Size.Fraction(0.6f)).Height(Size.Fit().Min(Size.Full()))
    ).Gap(16);
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

      // Read first row for headers
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

      // Scan remaining
      while (reader.Read())
      {
        rowCount++;
        if (reader.FieldCount > fieldCount) fieldCount = reader.FieldCount;
      }

      var sheetAnalysis = new SheetAnalysis
      {
        Name = sheetName,
        FieldCount = fieldCount,
        RowCount = rowCount,
        Headers = headers,
        MergeCellsCount = reader.MergeCells?.Length ?? 0
      };

      // Fix for CSV
      if (fileInfo.Extension.ToLowerInvariant() == ".csv")
      {
        sheetAnalysis.MergeCellsCount = 0;
      }

      analysis.Sheets.Add(sheetAnalysis);

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
