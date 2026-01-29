using Ivy.Shared;
using Ivy.Hooks;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.IO;
using ArtistInsightTool.Apps.Views;
using ArtistInsightTool.Apps.Services;

namespace ArtistInsightTool.Apps.Tables;

public class TemplateDataViewSheet(int templateId, Action onClose) : ViewBase
{
  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var rows = UseState<List<DynamicRow>>([]);
    var columns = UseState<DataTableColumn[]>([]);
    var isLoading = UseState(true);
    var error = UseState("");
    var templateName = UseState("Template Data");

    // Helper to extract year/quarter weight for sorting
    int GetTimeframeWeight(int? year, string? quarter)
    {
      int y = year ?? 0;
      int q = quarter switch
      {
        "Q1" => 1,
        "Q2" => 2,
        "Q3" => 3,
        "Q4" => 4,
        _ => 0
      };
      return (y * 10) + q;
    }

    double ConvertToDouble(object? val)
    {
      if (val == null) return 0;
      if (val is double d) return d;
      if (val is int i) return i;
      if (val is long l) return l;
      if (val is decimal dec) return (double)dec;
      if (val is JsonElement el && el.ValueKind == JsonValueKind.Number) return el.GetDouble();
      if (double.TryParse(val?.ToString(), out var parsed)) return parsed;
      return 0;
    }

    UseEffect(async () =>
    {
      try
      {
        await using var db = factory.CreateDbContext();
        var template = await db.ImportTemplates
            .Include(t => t.RevenueEntries)
            .FirstOrDefaultAsync(t => t.Id == templateId);

        if (template == null)
        {
          error.Set("Template not found.");
          isLoading.Set(false);
          return;
        }

        templateName.Set(template.Name);

        if (template.RevenueEntries == null || template.RevenueEntries.Count == 0)
        {
          error.Set("No data uploads found for this template.");
          isLoading.Set(false);
          return;
        }

        var mappings = template.GetMappings();
        string? GetHeader(string field) => mappings.FirstOrDefault(x => x.Value == field).Key;

        // Determine Aggregation Mode: Concat if date exists, Merge otherwise
        bool hasTimeframe = template.RevenueEntries.Any(e => e.Year != null || !string.IsNullOrEmpty(e.Quarter))
                           || !string.IsNullOrEmpty(GetHeader("TransactionDate"));

        // Sort entries by timeframe: Year then Quarter
        var sortedEntries = template.RevenueEntries
            .OrderBy(e => GetTimeframeWeight(e.Year, e.Quarter))
            .ToList();

        List<Dictionary<string, object?>> allDataRows = [];
        List<string>? headers = null;

        foreach (var entry in sortedEntries)
        {
          if (string.IsNullOrEmpty(entry.JsonData)) continue;

          try
          {
            using var doc = JsonDocument.Parse(entry.JsonData);
            var root = doc.RootElement;
            List<Dictionary<string, object?>> entryRows = [];

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
              var first = root[0];
              bool isObj = first.ValueKind == JsonValueKind.Object;
              bool hasFile = isObj && (first.TryGetProperty("FileName", out _) || first.TryGetProperty("fileName", out _));

              if (isObj && hasFile)
              {
                foreach (var sheet in root.EnumerateArray())
                {
                  if (sheet.TryGetProperty("Rows", out var rowsProp) || sheet.TryGetProperty("rows", out rowsProp))
                  {
                    var sheetRows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(rowsProp.GetRawText());
                    if (sheetRows != null) entryRows.AddRange(sheetRows);
                  }
                }
              }
              else
              {
                entryRows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(entry.JsonData) ?? [];
              }
            }

            if (entryRows.Count > 0)
            {
              if (headers == null) headers = entryRows[0].Keys.ToList();
              allDataRows.AddRange(entryRows);
            }
          }
          catch { }
        }

        if (allDataRows.Count == 0 || headers == null)
        {
          error.Set("No readable rows found in the associated uploads.");
          isLoading.Set(false);
          return;
        }

        List<Dictionary<string, object?>> finalRows;
        if (hasTimeframe)
        {
          finalRows = allDataRows;
        }
        else
        {
          // MERGE logic: Group by non-numeric, Sum numeric
          var numericKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Net", "Gross", "Amount", "Quantity", "Total", "Price" };
          var netH = GetHeader("Net"); if (!string.IsNullOrEmpty(netH)) numericKeys.Add(netH);
          var grossH = GetHeader("Gross"); if (!string.IsNullOrEmpty(grossH)) numericKeys.Add(grossH);
          var amountH = GetHeader("Amount"); if (!string.IsNullOrEmpty(amountH)) numericKeys.Add(amountH);
          var qtyH = GetHeader("Quantity"); if (!string.IsNullOrEmpty(qtyH)) numericKeys.Add(qtyH);

          var grouped = new Dictionary<string, Dictionary<string, object?>>();

          foreach (var row in allDataRows)
          {
            var keyParts = headers.Where(h => !numericKeys.Contains(h))
                                 .Select(h => row.GetValueOrDefault(h)?.ToString() ?? "");
            var groupKey = string.Join("|", keyParts);

            if (!grouped.TryGetValue(groupKey, out var aggRow))
            {
              aggRow = new Dictionary<string, object?>(row);
              grouped[groupKey] = aggRow;
            }
            else
            {
              foreach (var numKey in numericKeys.Intersect(headers))
              {
                var currentVal = ConvertToDouble(aggRow.GetValueOrDefault(numKey));
                var newVal = ConvertToDouble(row.GetValueOrDefault(numKey));
                aggRow[numKey] = currentVal + newVal;
              }
            }
          }
          finalRows = grouped.Values.ToList();
        }

        var cols = headers.Select((h, i) => new DataTableColumn
        {
          Name = $"Col{i}",
          Header = h,
          Order = i,
          Sortable = true,
          Filterable = true,
          ColType = ColType.Text
        }).ToArray();

        var dRows = finalRows.Select(d =>
        {
          var r = new DynamicRow();
          int i = 0;
          foreach (var h in headers)
          {
            r.SetValue(i, d.GetValueOrDefault(h));
            i++;
          }
          return r;
        }).ToList();

        columns.Set(cols);
        rows.Set(dRows);
      }
      catch (Exception ex)
      {
        error.Set($"Error aggregation data: {ex.Message}");
      }
      finally
      {
        isLoading.Set(false);
      }
    }, []);

    var config = new DataTableConfig
    {
      FreezeColumns = 1,
      ShowGroups = false,
      ShowIndexColumn = true,
      ShowSearch = true,
      AllowSorting = true,
      AllowFiltering = true,
      ShowVerticalBorders = true
    };

    var content = Layout.Vertical().Height(Size.Full())
        .Add(isLoading.Value ? Layout.Center().Add(Text.Label("Aggregating Template Data...")) :
             !string.IsNullOrEmpty(error.Value) ? Layout.Center().Add(Text.Label(error.Value).Color(Colors.Red)) :
             Layout.Vertical().Height(Size.Full()).Add(new DataTableView(rows.Value.AsQueryable(), Size.Full(), Size.Full(), columns.Value, config))
        );

    return new Sheet(
        _ => onClose(),
        content,
        $"Historical Data: {templateName.Value}",
        isLoading.Value ? "Loading..." : $"{rows.Value.Count} Total Rows"
    ).Width(Size.Full());
  }
}
