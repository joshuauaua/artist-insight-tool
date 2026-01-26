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
    var templatesQuery = UseQuery<List<ImportTemplate>, string>("ImportTemplates", async (ct) =>
    {
      await using var db = factory.CreateDbContext();
      return await db.ImportTemplates.ToListAsync(ct);
    }, tags: ["ImportTemplates"]);

    // --- Analysis State ---
    // --- Analysis State ---
    var filePaths = UseState<List<CurrentFile>>(() => new());
    var isAnalyzing = UseState(false);
    // var matchedTemplate = UseState<ImportTemplate?>(() => null); // Moved to CurrentFile
    // var isNewTemplate = UseState(false); // Moved to CurrentFile
    // var parsedData = UseState<List<Dictionary<string, object?>>>([]); // Moved to CurrentFile



    // --- Template Creation State ---




    // --- Upload Logic ---
    var uploadState = UseState<FileUpload<byte[]>?>();
    var templateTargetFileId = UseState<string?>(() => null); // Explicit lambda to avoid ambiguity

    var uploadContext = this.UseUpload(MemoryStreamUploadHandler.Create(uploadState))
        .Accept(".xlsx,.xls,.csv")
        .MaxFileSize(50 * 1024 * 1024);

    // Load templates on mount


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
    // Analysis Logic
    UseEffect(() =>
    {
      if (isAnalyzing.Value) return;

      var files = filePaths.Value;
      var templates = templatesQuery.Value ?? [];
      bool changed = false;
      var list = files.ToList();

      // Re-match logic
      for (int i = 0; i < list.Count; i++)
      {
        var f = list[i];
        if (f.Status == "Analyzed" && f.MatchedTemplate == null)
        {
          if (f.Analysis?.Sheets.Count > 0)
          {
            var firstSheetHeaders = f.Analysis.Sheets[0].Headers;
            var jsonHeaders = JsonSerializer.Serialize(firstSheetHeaders);
            var match = templates.FirstOrDefault(t => t.HeadersJson == jsonHeaders);
            if (match != null)
            {
              list[i] = f with { MatchedTemplate = match, IsNewTemplate = false };
              changed = true;
            }
          }
        }
      }

      if (changed)
      {
        filePaths.Set(list);
        return; // Skip analysis triggering if we just updated state
      }

      var pending = files.FirstOrDefault(f => f.Status == "Pending");

      if (pending != null)
      {
        isAnalyzing.Set(true);

        Task.Run(async () =>
        {
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
              var match = (templatesQuery.Value ?? []).FirstOrDefault(t => t.HeadersJson == jsonHeaders);

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
        });
      }
    });



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

    IView RenderFileRow(CurrentFile f)
    {
      return Layout.Horizontal().Gap(10).Align(Align.Center).Padding(10)
            .Add(new Icon(Icons.FileText).Size(20))
            .Add(Layout.Vertical()
                .Add(Text.Label(f.OriginalName))
                .Add(Text.Muted($"{FormatFileSize(f.FileSize)} - {f.Status}"))
            )
            .Add(Layout.Horizontal().Grow())
            .Add(f.MatchedTemplate != null ? Text.Success(f.MatchedTemplate.Name) :
                 f.Status == "Analyzed" ?
                    Layout.Horizontal().Gap(5).Align(Align.Center)
                          .Add(Text.Warning("No Template"))
                          .Add(new Button("Create", () =>
                          {
                            templateTargetFileId.Set(f.Id);
                            activeMode.Set(AnalyzerMode.TemplateCreation);
                          }).Variant(ButtonVariant.Secondary))
                 : Text.Muted("-"))
            .Add(new Button("", () =>
            {
              var l = filePaths.Value.ToList();
              l.RemoveAll(x => x.Id == f.Id);
              filePaths.Set(l);
            }).Variant(ButtonVariant.Ghost).Icon(Icons.Trash));
    }

    // --- Render Methods ---

    object RenderHome()
    {
      var files = filePaths.Value ?? new List<CurrentFile>();
      var hasFiles = files.Count > 0;
      var isProcessing = isAnalyzing.Value;
      var ready = files.All(f => f.Status == "Analyzed");

      var showDialog = (activeMode.Value != AnalyzerMode.Home && activeMode.Value != AnalyzerMode.Upload)
                       || (activeMode.Value == AnalyzerMode.Upload && ready);

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

      if (activeMode.Value == AnalyzerMode.TemplateCreation)
      {
        var targetId = templateTargetFileId.Value;
        var targetFile = filePaths.Value.FirstOrDefault(f => f.Id == targetId) ?? GetActiveFile();
        return new TemplateCreatorSheet(
            targetFile,
            () =>
            {
              // No manual reload needed, validated by tag
              activeMode.Set(AnalyzerMode.Home);
            },
            () => activeMode.Set(AnalyzerMode.Home)
        );
      }

      var container = Layout.Vertical().Height(Size.Full()).Width(Size.Full()).Align(Align.Center).Padding(20);

      var content = Layout.Vertical().Gap(20).Align(Align.Center);
      content.Add(new Icon(Icons.Sheet).Size(48));
      content.Add(Text.H4("Import Data"));
      content.Add(uploadState.ToFileInput(uploadContext).Placeholder("Select Files (Excel/CSV)").Width(200));

      if (isProcessing)
      {
        content.Add(Text.Label("Processing..."));
      }

      if (hasFiles)
      {
        var fileList = Layout.Vertical().Gap(10).Width(Size.Full());
        foreach (var f in files)
        {
          fileList.Add(RenderFileRow(f));
        }
        content.Add(fileList);
      }

      if (hasFiles && ready && !isProcessing)
      {
        content.Add(Layout.Horizontal().Gap(10)
            .Add(new Button("Clear All", () => filePaths.Set(new List<CurrentFile>())).Variant(ButtonVariant.Outline))
            .Add(new Button("Import All", () =>
            {
              if (files.Any(f => f.MatchedTemplate == null))
              {
                client.Toast("Templates missing for some files", "Warning");
                return;
              }
              activeMode.Set(AnalyzerMode.Upload);
            }).Variant(ButtonVariant.Primary))
        );
      }

      container.Add(content);

      if (showDialog)
      {
        container.Add(new Dialog(
             _ =>
             {
               if (activeMode.Value != AnalyzerMode.Home) activeMode.Set(AnalyzerMode.Home);
             },
             new DialogHeader(GetDialogTitle()),
             new DialogBody(
                  activeMode.Value == AnalyzerMode.Analysis ? RenderAnalysisContent() :
                  activeMode.Value == AnalyzerMode.Annex ? new AnnexSheet(GetActiveFile(), () => activeMode.Set(AnalyzerMode.Home)) :
                  activeMode.Value == AnalyzerMode.DataView ? RenderDataTableView() :
                  activeMode.Value == AnalyzerMode.Preview ? RenderPreviewContent() :
                  activeMode.Value == AnalyzerMode.Upload ? new ImportConfirmationSheet(files.Where(f => f.Status == "Analyzed" && f.MatchedTemplate != null).ToList(), () => { filePaths.Set(new List<CurrentFile>()); activeMode.Set(AnalyzerMode.Home); }, () => activeMode.Set(AnalyzerMode.Home)) :
                  Text.Muted("Not implemented view")
             ),
             new DialogFooter()
        ));
      }

      return new Sheet(
          _ => { _onClose(); return ValueTask.CompletedTask; },
          container,
          "Import Data",
          "Upload and process multiple financial data files."
      ).Width(Size.Full());
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
  public static List<Dictionary<string, object?>> ParseData(string filePath, ImportTemplate template)
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
