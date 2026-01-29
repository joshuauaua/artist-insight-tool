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
}
