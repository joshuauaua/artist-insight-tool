using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArtistInsight.Backend.Data;
using ArtistInsight.Backend.Models;
using System.Globalization;
using System.Text.Json;

namespace ArtistInsight.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
  private readonly ArtistInsightContext _context;

  public DashboardController(ArtistInsightContext context)
  {
    _context = context;
  }

  [HttpGet("metrics/revenue-total")]
  public async Task<ActionResult<MetricDto>> GetTotalRevenue([FromQuery] DateTime from, [FromQuery] DateTime to)
  {
    var totalAmount = await _context.RevenueEntries
        .Where(r => r.RevenueDate >= from && r.RevenueDate <= to)
        .SumAsync(r => r.Amount);


    return new MetricDto(
        Value: totalAmount.ToString("C0", CultureInfo.GetCultureInfo("sv-SE")),
        Trend: 0,
        GoalProgress: 0,
        GoalValue: null,
        NumericValue: (double)totalAmount
    );
  }

  [HttpGet("metrics/revenue-by-asset")]
  public async Task<ActionResult<IEnumerable<PieChartSegmentDto>>> GetRevenueByAsset([FromQuery] DateTime from, [FromQuery] DateTime to)
  {
    Console.WriteLine($"[DEBUG] GetRevenueByAsset called from {from} to {to}");
    try
    {
      var data = await _context.AssetRevenues
          .Include(ar => ar.RevenueEntry)
          .Include(ar => ar.Asset)
          .Where(ar => ar.RevenueEntry.RevenueDate >= from && ar.RevenueEntry.RevenueDate <= to)
          .GroupBy(ar => ar.Asset.Name)
          .Select(g => new
          {
            Label = g.Key,
            Value = g.Sum(x => x.Amount)
          })
          .OrderByDescending(x => x.Value)
          .Take(5)
          .ToListAsync();

      Console.WriteLine($"[DEBUG] Found {data.Count} assets. Top: {data.FirstOrDefault()?.Label} ({data.FirstOrDefault()?.Value})");

      return data.Select(x => new PieChartSegmentDto(x.Label, (double)x.Value)).ToList();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[ERROR] GetRevenueByAsset failed: {ex.Message} {ex.StackTrace}");
      return StatusCode(500, ex.Message);
    }
  }

  [HttpGet("charts/revenue-by-source")]
  public async Task<ActionResult<IEnumerable<PieChartSegmentDto>>> GetRevenueBySource([FromQuery] DateTime from, [FromQuery] DateTime to)
  {
    try
    {
      var rawEntries = await _context.RevenueEntries
          .Where(r => r.RevenueDate >= from && r.RevenueDate <= to && r.JsonData != null && r.JsonData != "")
          .Select(r => new { r.JsonData })
          .ToListAsync();

      var storeTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

      foreach (var entry in rawEntries)
      {
        if (string.IsNullOrEmpty(entry.JsonData)) continue;

        try
        {
          using var doc = JsonDocument.Parse(entry.JsonData);
          if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

          foreach (var sheet in doc.RootElement.EnumerateArray())
          {
            if (sheet.TryGetProperty("Rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
            {
              foreach (var row in rows.EnumerateArray())
              {
                // Find Store Name
                string? store = null;
                if (row.TryGetProperty("Sale Store Name", out var pStore)) store = pStore.GetString();
                else if (row.TryGetProperty("Store", out pStore)) store = pStore.GetString();
                else if (row.TryGetProperty("DSP", out pStore)) store = pStore.GetString();
                else if (row.TryGetProperty("Source", out pStore)) store = pStore.GetString();

                if (string.IsNullOrWhiteSpace(store)) continue;

                // Find separate Amount
                double amount = 0;
                if (row.TryGetProperty("Sale Net Receipts", out var pNet))
                {
                  var s = pNet.GetString();
                  if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) amount = d;
                }
                else if (row.TryGetProperty("Net", out pNet))
                {
                  var s = pNet.GetString();
                  if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) amount = d;
                }
                else if (row.TryGetProperty("Amount", out pNet))
                {
                  var s = pNet.GetString();
                  if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) amount = d;
                }
                else if (row.TryGetProperty("Royalty", out pNet))
                {
                  var s = pNet.GetString();
                  if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) amount = d;
                }

                if (amount > 0)
                {
                  if (!storeTotals.ContainsKey(store)) storeTotals[store] = 0;
                  storeTotals[store] += amount;
                }
              }
            }
          }
        }
        catch { }
      }

      var result = storeTotals
          .Select(kv => new PieChartSegmentDto(kv.Key, kv.Value))
          .OrderByDescending(x => x.Value)
          .ToList();

      return result;
    }
    catch (Exception ex)
    {
      return StatusCode(500, ex.Message);
    }
  }
  [HttpGet("charts/stream-history")]
  public async Task<ActionResult<IEnumerable<PieChartSegmentDto>>> GetStreamHistory([FromQuery] DateTime from, [FromQuery] DateTime to)
  {
    try
    {
      var rawEntries = await _context.RevenueEntries
          .Where(r => r.RevenueDate >= from && r.RevenueDate <= to && r.JsonData != null && r.JsonData != "")
          .Select(r => new { r.RevenueDate, r.JsonData })
          .ToListAsync();

      var monthlyStreams = new Dictionary<DateTime, double>();

      foreach (var entry in rawEntries)
      {
        if (string.IsNullOrEmpty(entry.JsonData)) continue;

        double entryStreams = 0;
        try
        {
          using var doc = JsonDocument.Parse(entry.JsonData);
          if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

          foreach (var sheet in doc.RootElement.EnumerateArray())
          {
            if (sheet.TryGetProperty("Rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
            {
              foreach (var row in rows.EnumerateArray())
              {
                double qty = 0;
                if (row.TryGetProperty("Quantity", out var pQty))
                {
                  var s = pQty.GetString();
                  if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) qty = d;
                }
                else if (row.TryGetProperty("Units", out pQty))
                {
                  var s = pQty.GetString();
                  if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) qty = d;
                }
                else if (row.TryGetProperty("Count", out pQty))
                {
                  var s = pQty.GetString();
                  if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) qty = d;
                }

                entryStreams += qty;
              }
            }
          }
        }
        catch { }

        if (entryStreams > 0)
        {
          var monthDate = new DateTime(entry.RevenueDate.Year, entry.RevenueDate.Month, 1);
          if (!monthlyStreams.ContainsKey(monthDate)) monthlyStreams[monthDate] = 0;
          monthlyStreams[monthDate] += entryStreams;
        }
      }

      var result = monthlyStreams
          .Select(kv => new PieChartSegmentDto(kv.Key.ToString("MMM yyyy", CultureInfo.InvariantCulture), kv.Value))
          .OrderBy(x => DateTime.ParseExact(x.Label, "MMM yyyy", CultureInfo.InvariantCulture))
          .ToList();

      return result;
    }
    catch (Exception ex)
    {
      return StatusCode(500, ex.Message);
    }
  }

  [HttpGet("charts/revenue-by-asset-history")]
  public async Task<ActionResult<List<Dictionary<string, object>>>> GetRevenueByAssetHistory([FromQuery] DateTime from, [FromQuery] DateTime to)
  {
    try
    {
      var rawEntries = await _context.RevenueEntries
          .Where(r => r.RevenueDate >= from && r.RevenueDate <= to && r.JsonData != null && r.JsonData != "")
          .Select(r => new { r.RevenueDate, r.JsonData })
          .ToListAsync();

      // Dictionary<MonthDate, Dictionary<AssetName, Amount>>
      var timeline = new Dictionary<DateTime, Dictionary<string, double>>();
      var allAssetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      foreach (var entry in rawEntries)
      {
        if (string.IsNullOrEmpty(entry.JsonData)) continue;

        try
        {
          using var doc = JsonDocument.Parse(entry.JsonData);
          if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

          foreach (var sheet in doc.RootElement.EnumerateArray())
          {
            if (sheet.TryGetProperty("Rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
            {
              foreach (var row in rows.EnumerateArray())
              {
                // 1. Get Asset Name
                string? assetName = null;
                if (row.TryGetProperty("Asset Title", out var pName)) assetName = pName.GetString();
                if (string.IsNullOrWhiteSpace(assetName) && row.TryGetProperty("Product Title", out pName)) assetName = pName.GetString();

                if (string.IsNullOrWhiteSpace(assetName)) continue; // Skip if no name found

                // 2. Get Amount
                double amount = 0;
                if (row.TryGetProperty("Net", out var pNet))
                {
                  var s = pNet.GetString();
                  if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) amount = d;
                }
                else if (row.TryGetProperty("Amount", out pNet))
                {
                  var s = pNet.GetString();
                  if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) amount = d;
                }
                else if (row.TryGetProperty("Sale Net Receipts", out pNet))
                {
                  var s = pNet.GetString();
                  if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) amount = d;
                }

                if (amount > 0)
                {
                  var monthDate = new DateTime(entry.RevenueDate.Year, entry.RevenueDate.Month, 1);
                  if (!timeline.ContainsKey(monthDate)) timeline[monthDate] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                  if (!timeline[monthDate].ContainsKey(assetName)) timeline[monthDate][assetName] = 0;
                  timeline[monthDate][assetName] += amount;

                  allAssetNames.Add(assetName);
                }
              }
            }
          }
        }
        catch { }
      }

      var result = timeline
          .OrderBy(kv => kv.Key)
          .Select(kv =>
          {
            var dict = new Dictionary<string, object>
              {
                  { "Month", kv.Key.ToString("MMM yyyy", CultureInfo.InvariantCulture) },
                  { "_SortDate", kv.Key } // Helper for sorting if needed on frontend, though we order here
              };
            foreach (var asset in allAssetNames)
            {
              dict[asset] = kv.Value.ContainsKey(asset) ? kv.Value[asset] : 0.0;
            }
            return dict;
          })
          .ToList();

      return result;
    }
    catch (Exception ex)
    {
      return StatusCode(500, ex.Message);
    }
  }
}

