using Ivy.Shared;
using System.IO;
using ExcelDataReader;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Linq.Expressions;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Table, title: "Excel Import", path: ["Integrations", "Excel Import"])]
public class CsvHelperApp : ViewBase
{
  public CsvHelperApp()
  {
    // Required for ExcelDataReader to support older formats and certain encodings
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
  }

  public override object? Build()
  {
    var client = UseService<IClientProvider>();

    // State for dynamic data
    var headers = UseState<List<string>>([]);
    var rows = UseState<List<Dictionary<string, object>>>([]);

    // Import
    var uploadState = UseState<FileUpload<byte[]>?>();
    var uploadContext = this.UseUpload(MemoryStreamUploadHandler.Create(uploadState))
        .Accept(".csv, .xlsx, .xls")
        .MaxFileSize(20 * 1024 * 1024);

    // Process Upload
    UseEffect(() =>
    {
      if (uploadState.Value?.Content is byte[] bytes && bytes.Length > 0)
      {
        try
        {
          using var stream = new MemoryStream(bytes);
          // Auto-detect format (Excel or CSV)
          using var reader = ExcelReaderFactory.CreateReader(stream);

          var result = reader.AsDataSet(new ExcelDataSetConfiguration()
          {
            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
            {
              UseHeaderRow = true
            }
          });

          if (result.Tables.Count > 0)
          {
            var dataTable = result.Tables[0];

            // Extract Headers
            var newHeaders = new List<string>();
            foreach (DataColumn col in dataTable.Columns)
            {
              newHeaders.Add(col.ColumnName);
            }

            // Extract Rows
            var newRows = new List<Dictionary<string, object>>();
            foreach (DataRow dr in dataTable.Rows)
            {
              var dict = new Dictionary<string, object>();
              foreach (DataColumn col in dataTable.Columns)
              {
                dict[col.ColumnName] = dr[col];
              }
              newRows.Add(dict);
            }

            // Use .Value assignment to avoid ambiguity
            headers.Value = newHeaders;
            rows.Value = newRows;
            client.Toast($"Imported {newRows.Count} rows with {newHeaders.Count} columns.");
          }
        }
        catch (Exception ex)
        {
          client.Toast($"Import failed: {ex.Message}");
        }
      }
    }, [uploadState]);

    // Clear Data Action
    var clearData = new Action(() =>
    {
      headers.Value = [];
      rows.Value = [];
      uploadState.Value = null;
    });

    // Dynamic Markdown Preview Construction
    // Falling back to Markdown as Table/DataTable widgets require POCOs/Expressions
    var previewText = "Upload a file to view data";

    if (headers.Value.Count > 0)
    {
      var sb = new StringBuilder();

      // Markdown Table Header
      sb.Append("| ");
      sb.Append(string.Join(" | ", headers.Value));
      sb.AppendLine(" |");

      // Markdown Table Separator
      sb.Append("| ");
      sb.Append(string.Join(" | ", headers.Value.Select(_ => "---")));
      sb.AppendLine(" |");

      // Rows (Limit 50 for performance)
      var limit = 50;
      foreach (var row in rows.Value.Take(limit))
      {
        sb.Append("| ");
        foreach (var h in headers.Value)
        {
          var val = row.ContainsKey(h) ? row[h]?.ToString() ?? "" : "";

          // Sanitize for Markdown Table: escape pipes, remove newlines
          val = val.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");

          sb.Append(val);
          sb.Append(" | ");
        }
        sb.AppendLine();
      }

      if (rows.Value.Count > limit)
      {
        sb.AppendLine($"\n\n_... showing first {limit} of {rows.Value.Count} rows. Export to see all data._");
      }

      previewText = sb.ToString();
    }

    // CSV Export (Dynamic)
    var downloadUrl = this.UseDownload(
        async () =>
        {
          await using var ms = new MemoryStream();
          await using var writer = new StreamWriter(ms, leaveOpen: true);

          // Write Headers
          await writer.WriteLineAsync(string.Join(",", headers.Value.Select(h => $"\"{h}\"")));

          // Write Rows
          foreach (var row in rows.Value)
          {
            var values = headers.Value.Select(h =>
                {
                  var val = row.ContainsKey(h) ? row[h]?.ToString() ?? "" : "";
                  // Simple CSV escaping
                  return $"\"{val.Replace("\"", "\"\"")}\"";
                });
            await writer.WriteLineAsync(string.Join(",", values));
          }

          await writer.FlushAsync();
          ms.Position = 0;
          return ms.ToArray();
        },
        "text/csv",
        $"export-{DateTime.UtcNow:yyyy-MM-dd}.csv"
    );

    // UI Layout

    var controls = new Card(
        Layout.Vertical().Gap(10)
        .Add(Text.H3("File Import"))
        .Add(Text.Small("Supports .csv, .xlsx, .xls"))

        // File Input
        .Add(uploadState.ToFileInput(uploadContext).Placeholder("Select File"))

        .Add(new Separator())

        // Info
        .Add(Layout.Horizontal()
            .Add(Text.Small($"Columns: {headers.Value.Count}"))
            .Add(new Spacer())
            .Add(Text.Small($"Rows: {rows.Value.Count}"))
        )

        // Actions
        .Add(Layout.Horizontal().Gap(5)
            .Add(new Button("Clear").Variant(ButtonVariant.Outline).HandleClick(_ => clearData()).Width(Size.Fraction(0.5f)))
            .Add(new Button("Export CSV").Icon(Icons.Download).Url(downloadUrl.Value).Width(Size.Fraction(0.5f)))
        )
    ).Title("Import Control");

    // Render Markdown Preview inside the card
    return Layout.Vertical().Gap(10).Padding(10)
        .Add(controls)
        .Add(new Card(Text.Markdown(previewText)).Title("Data Preview").Height(Size.Fit().Min(400)));
  }
}
