using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArtistInsight.Backend.Data;
using ArtistInsight.Backend.Models;
using System.Globalization;

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
}
