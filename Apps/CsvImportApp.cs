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
    var factory = UseService<ArtistInsightToolContextFactory>();

    // State for dynamic data
    var headers = UseState<List<string>>([]);
    var rows = UseState<List<Dictionary<string, object>>>([]);

    // Import State
    var uploadState = UseState<FileUpload<byte[]>?>();
    var uploadContext = this.UseUpload(MemoryStreamUploadHandler.Create(uploadState))
        .Accept(".csv, .xlsx, .xls")
        .MaxFileSize(20 * 1024 * 1024);

    // Mapping State
    var dateColumn = UseState<string?>(() => null);
    var amountColumn = UseState<string?>(() => null);
    var descriptionColumn = UseState<string?>(() => null); // Optional description mapping

    // Metadata State
    var revenueSources = UseState<List<RevenueSource>>([]);
    var selectedSourceId = UseState<int?>(() => null);

    // Load Revenue Sources on Init
    UseEffect(async () =>
    {
      await using var db = factory.CreateDbContext();
      var sources = await db.RevenueSources.OrderBy(s => s.DescriptionText).ToListAsync();
      revenueSources.Set(sources);
      if (sources.Any())
      {
        selectedSourceId.Set(sources.First().Id);
      }
    }, [EffectTrigger.AfterInit()]);


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
            headers.Set(newHeaders);
            rows.Set(newRows);

            // Try to auto-map columns based on common names
            var dateCol = newHeaders.FirstOrDefault(h => h.ToLower().Contains("date") || h.ToLower().Contains("time"));
            var amountCol = newHeaders.FirstOrDefault(h => h.ToLower().Contains("amount") || h.ToLower().Contains("price") || h.ToLower().Contains("cost") || h.ToLower().Contains("value"));
            var descCol = newHeaders.FirstOrDefault(h => h.ToLower().Contains("description") || h.ToLower().Contains("details") || h.ToLower().Contains("memo"));

            if (dateCol != null) dateColumn.Set(dateCol);
            if (amountCol != null) amountColumn.Set(amountCol);
            if (descCol != null) descriptionColumn.Set(descCol);

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
      headers.Set([]);
      rows.Set([]);
      uploadState.Set((FileUpload<byte[]>?)null);
      dateColumn.Set((string?)null);
      amountColumn.Set((string?)null);
      descriptionColumn.Set((string?)null);
    });

    // Save Data Action
    var saveData = new Action(async () =>
    {
      if (rows.Value.Count == 0 || dateColumn.Value == null || amountColumn.Value == null || selectedSourceId.Value == null)
      {
        client.Toast("Please map required columns (Date, Amount) and select a Source.");
        return;
      }

      try
      {
        await using var db = factory.CreateDbContext();

        // Get default artist (assuming single artist context for now, or first one)
        var artist = await db.Artists.FirstOrDefaultAsync();
        if (artist == null)
        {
          // Create default artist if none exists? Or error?
          // For now, let's assume at least one artist exists or create a placeholder "Unknown"
          artist = new Artist { Name = "Main Artist", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
          db.Artists.Add(artist);
          await db.SaveChangesAsync();
        }

        var sourceId = selectedSourceId.Value.Value;
        var source = await db.RevenueSources.FindAsync(sourceId);
        var sourceName = source?.DescriptionText ?? "Unknown";

        int importedCount = 0;
        var entriesToAdd = new List<RevenueEntry>();

        foreach (var row in rows.Value)
        {
          // Extract Date
          DateTime revenueDate = DateTime.UtcNow;
          if (row.ContainsKey(dateColumn.Value) && row[dateColumn.Value] != null)
          {
            if (!DateTime.TryParse(row[dateColumn.Value].ToString(), out revenueDate))
            {
              // Try generic formats if needed, or skip/log error
              continue;
            }
          }
          else
          {
            continue; // Skip if no date
          }

          // Extract Amount
          decimal amount = 0;
          if (row.ContainsKey(amountColumn.Value) && row[amountColumn.Value] != null)
          {
            var amtStr = row[amountColumn.Value].ToString()?.Replace("$", "").Replace(",", "").Trim();
            if (!decimal.TryParse(amtStr, out amount))
            {
              continue; // Skip if invalid amount
            }
          }

          // Extract Description
          string description = "";
          if (descriptionColumn.Value != null && row.ContainsKey(descriptionColumn.Value))
          {
            description = row[descriptionColumn.Value]?.ToString() ?? "";
          }

          // Create Entry
          var entry = new RevenueEntry
          {
            ArtistId = artist.Id,
            SourceId = sourceId,
            RevenueDate = revenueDate,
            Amount = amount,
            Integration = "Import", // Or maybe use sourceName? keeping "Import" to distinguish or allow filtering by "manual import" vs "api"
            Description = string.IsNullOrWhiteSpace(description) ? $"Imported from {sourceName}" : description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
          };

          entriesToAdd.Add(entry);
          importedCount++;
        }

        if (entriesToAdd.Count > 0)
        {
          db.RevenueEntries.AddRange(entriesToAdd);
          await db.SaveChangesAsync();
          client.Toast($"Successfully added {importedCount} revenue entries.");
          clearData();
        }
        else
        {
          client.Toast("No valid rows found to import.");
        }

      }
      catch (Exception ex)
      {
        client.Toast($"Error saving data: {ex.Message}");
        Console.WriteLine(ex);
      }
    });


    // UI Components

    var mappingControls = Layout.Vertical().Gap(10).Padding(10);

    if (headers.Value.Count > 0)
    {
      var columns = headers.Value.Select(h => new Option<string>(h, h)).ToList();

      var sourceOptions = revenueSources.Value.Select(s => new Option<int?>(s.DescriptionText, s.Id)).ToList();

      mappingControls
          .Add(Text.H4("Map Columns"))
          .Add(Layout.Horizontal().Gap(10)
              .Add(Text.Label("Revenue Source").Width(150))
              .Add(selectedSourceId.ToSelectInput(sourceOptions).Width(Size.Full()))
          )
          .Add(Layout.Horizontal().Gap(10)
              .Add(Text.Label("Date Column").Width(150))
              .Add(dateColumn.ToSelectInput(columns).Width(Size.Full()))
          )
          .Add(Layout.Horizontal().Gap(10)
              .Add(Text.Label("Amount Column").Width(150))
              .Add(amountColumn.ToSelectInput(columns).Width(Size.Full()))
          )
          .Add(Layout.Horizontal().Gap(10)
              .Add(Text.Label("Description (Opt)").Width(150))
              .Add(descriptionColumn.ToSelectInput(columns).Placeholder("Select optional description...").Width(Size.Full()))
          )
          .Add(new Separator())
          .Add(new Button("Save to Revenue Table")
              .Icon(Icons.Save)

              .HandleClick(_ => saveData())
          );
    }

    // Preview Table
    var previewLayout = Layout.Vertical();
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

      // Rows (Limit 20 for preview)
      var limit = 20;
      foreach (var row in rows.Value.Take(limit))
      {
        sb.Append("| ");
        foreach (var h in headers.Value)
        {
          var val = row.ContainsKey(h) ? row[h]?.ToString() ?? "" : "";
          val = val.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
          sb.Append(val);
          sb.Append(" | ");
        }
        sb.AppendLine();
      }
      if (rows.Value.Count > limit)
      {
        sb.AppendLine($"\n\n_... showing first {limit} of {rows.Value.Count} rows._");
      }

      previewLayout.Add(new Card(Text.Markdown(sb.ToString())).Title($"Data Preview ({rows.Value.Count} rows)"));
    }
    else
    {
      previewLayout.Add(new Card(Text.Markdown("Upload a file to preview data.")).Title("Data Preview"));
    }


    // Layout
    var controls = new Card(
        Layout.Vertical().Gap(10)
        .Add(Text.H3("File Import"))
        .Add(Text.Small("Supports .csv, .xlsx, .xls"))
        .Add(uploadState.ToFileInput(uploadContext).Placeholder("Select File"))
        .Add(new Separator())
        .Add(Layout.Horizontal()
            .Add(Text.Small($"Columns: {headers.Value.Count}"))
            .Add(new Spacer())
            .Add(Text.Small($"Rows: {rows.Value.Count}"))
        )
         .Add(Layout.Horizontal().Gap(5)
            .Add(new Button("Clear").Variant(ButtonVariant.Outline).HandleClick(_ => clearData()).Width(Size.Fraction(0.5f)))
        )
    ).Title("Import Control");

    return Layout.Vertical().Gap(20).Padding(20)
        .Add(Layout.Horizontal().Gap(20)
            .Add(Layout.Vertical().Width(400).Gap(20)
                .Add(controls)
                .Add(headers.Value.Count > 0 ? new Card(mappingControls).Title("Configuration") : Layout.Vertical())
            )
            .Add(Layout.Vertical().Width(Size.Full())
                .Add(previewLayout)
            )
        );
  }
}
