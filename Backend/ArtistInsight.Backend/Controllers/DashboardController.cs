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
  public async Task<MetricDto> GetTotalRevenue(DateTime from, DateTime to)
  {
    var total = await _context.RevenueEntries
        .Where(r => r.RevenueDate >= from && r.RevenueDate <= to)
        .SumAsync(r => r.Amount);

    // Simple trend comparison (vs previous equal period)
    var period = to - from;
    var prevFrom = from - period;
    var prevTo = from.AddDays(-1);

    var prevTotal = await _context.RevenueEntries
         .Where(r => r.RevenueDate >= prevFrom && r.RevenueDate <= prevTo)
         .SumAsync(r => r.Amount);

    double? trend = prevTotal > 0 ? (double)(total - prevTotal) / (double)prevTotal : null; // Ratio

    var goal = prevTotal * 1.1m;
    double? goalProgress = goal > 0 ? (double)(total / goal) : null;

    return new MetricDto(total.ToString("C0"), trend, goalProgress, goal.ToString("C0"));
  }

  [HttpGet("metrics/growth-rate")]
  public async Task<MetricDto> GetGrowthRate(DateTime from, DateTime to)
  {
    var currentPeriodRevenue = (double)await _context.RevenueEntries
         .Where(re => re.RevenueDate >= from && re.RevenueDate <= to)
         .SumAsync(re => re.Amount);

    var periodLength = to - from;
    var previousFromDate = from.AddDays(-periodLength.TotalDays);
    var previousToDate = from.AddDays(-1);

    var previousPeriodRevenue = (double)await _context.RevenueEntries
        .Where(re => re.RevenueDate >= previousFromDate && re.RevenueDate <= previousToDate)
        .SumAsync(re => re.Amount);

    if (previousPeriodRevenue == 0)
    {
      return new MetricDto(currentPeriodRevenue.ToString("N2"), null, null, null);
    }

    double? trend = (currentPeriodRevenue - previousPeriodRevenue) / previousPeriodRevenue * 100;

    var goal = previousPeriodRevenue * 1.1;
    double? goalAchievement = goal > 0 ? (double?)(currentPeriodRevenue / goal) : null;

    return new MetricDto(
        currentPeriodRevenue.ToString("N2"),
        trend,
        goalAchievement,
        goal.ToString("N2")
    );
  }

  [HttpGet("charts/revenue-by-source")]
  public async Task<List<PieChartSegmentDto>> GetRevenueBySource(DateTime from, DateTime to)
  {
    var data = await _context.RevenueEntries
        .Where(r => r.RevenueDate >= from && r.RevenueDate <= to)
        .Include(r => r.Source)
        .GroupBy(r => r.Source.DescriptionText)
        .Select(g => new
        {
          Label = g.Key,
          Value = (double)g.Sum(r => r.Amount)
        })
        .ToListAsync();

    return data.Select(d => new PieChartSegmentDto(d.Label, d.Value)).ToList();
  }

  [HttpGet("charts/revenue-by-album")]
  public async Task<List<PieChartSegmentDto>> GetRevenueByAlbum(DateTime from, DateTime to)
  {
    // AlbumId can be null, handle mapping
    var data = await _context.RevenueEntries
        .Where(r => r.RevenueDate >= from && r.RevenueDate <= to)
        .Include(r => r.Album)
        .GroupBy(r => r.Album != null ? r.Album.Title : "Single/Other")
        .Select(g => new
        {
          Label = g.Key,
          Value = (double)g.Sum(r => r.Amount)
        })
        .ToListAsync();

    return data.Select(d => new PieChartSegmentDto(d.Label, d.Value)).ToList();
  }

  [HttpGet("charts/revenue-trend")]
  public async Task<List<LineChartPointDto>> GetRevenueTrend(DateTime from, DateTime to)
  {
    var data = await _context.RevenueEntries
       .Where(r => r.RevenueDate >= from && r.RevenueDate <= to)
       .GroupBy(r => r.RevenueDate.Date)
       .Select(g => new
       {
         Date = g.Key,
         Value = (double)g.Sum(r => r.Amount)
       })
       .OrderBy(x => x.Date)
       .ToListAsync();

    return data.Select(d => new LineChartPointDto(d.Date, d.Value)).ToList();
  }

  [HttpGet("lists/top-tracks")]
  public async Task<List<TrackPerformanceDto>> GetTopTracks(DateTime from, DateTime to, int count = 5)
  {
    var data = await _context.RevenueEntries
        .Where(r => r.RevenueDate >= from && r.RevenueDate <= to && r.TrackId != null)
        .Include(r => r.Track)
        .GroupBy(r => r.Track!.Title)
        .Select(g => new TrackPerformanceDto(
            g.Key,
            (double)g.Sum(r => r.Amount),
            0 // Streams count not in RevenueEntry typically, unless mocked
        ))
        .OrderByDescending(x => x.Revenue)
        .Take(count)
        .ToListAsync();

    return data;
  }

  [HttpGet("charts/top-performing-tracks-points")]
  public async Task<List<TopTrackPointDto>> GetTopPerformingTracksPoints(DateTime from, DateTime to)
  {
    var data = await _context.RevenueEntries
        .Where(r => r.RevenueDate >= from && r.RevenueDate <= to)
        .Include(r => r.Track)
        .GroupBy(r => new { Title = r.Track != null ? r.Track.Title : "Unknown", r.RevenueDate })
        .Select(g => new
        {
          g.Key.Title,
          g.Key.RevenueDate,
          Revenue = (double)g.Sum(r => r.Amount)
        })
        .OrderByDescending(x => x.Revenue)
        .Take(5)
        .ToListAsync();

    return data.Select(d => new TopTrackPointDto(d.Title, d.RevenueDate, d.Revenue)).ToList();
  }

  [HttpGet("metrics/tracks-created")]
  public async Task<MetricDto> GetTracksCreated(DateTime from, DateTime to)
  {
    var current = await _context.Tracks
        .Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
        .CountAsync();

    var period = to - from;
    var prevFrom = from - period;
    var prevTo = from.AddDays(-1);

    var prev = await _context.Tracks
        .Where(t => t.CreatedAt >= prevFrom && t.CreatedAt <= prevTo)
        .CountAsync();

    double? trend = prev > 0 ? (double)(current - prev) / (double)prev : null;
    var goal = prev * 1.1;
    double? goalProgress = goal > 0 ? (double)(current / goal) : null;

    return new MetricDto(current.ToString("N0"), trend, goalProgress, goal.ToString("N0"));
  }

  [HttpGet("metrics/albums-released")]
  public async Task<MetricDto> GetAlbumsReleased(DateTime from, DateTime to)
  {
    var current = await _context.Albums
        .Where(a => a.ReleaseDate >= from && a.ReleaseDate <= to)
        .CountAsync();

    var period = to - from;
    var prevFrom = from - period;
    var prevTo = from.AddDays(-1);

    var prev = await _context.Albums
        .Where(a => a.ReleaseDate >= prevFrom && a.ReleaseDate <= prevTo)
        .CountAsync();

    double? trend = prev > 0 ? (double)(current - prev) / (double)prev : null;
    var goal = prev * 1.1;
    double? goalProgress = goal > 0 ? (double)(current / goal) : null;

    return new MetricDto(current.ToString("N0"), trend, goalProgress, goal.ToString("N0"));
  }

  [HttpGet("metrics/avg-track-duration")]
  public async Task<MetricDto> GetAvgTrackDuration(DateTime from, DateTime to)
  {
    var tracks = await _context.Tracks
        .Where(t => t.CreatedAt >= from && t.CreatedAt <= to && t.Duration != null)
        .Select(t => (double)t.Duration!)
        .ToListAsync();

    var current = tracks.Any() ? tracks.Average() : 0;

    var period = to - from;
    var prevFrom = from - period;
    var prevTo = from.AddDays(-1);

    var prevTracks = await _context.Tracks
         .Where(t => t.CreatedAt >= prevFrom && t.CreatedAt <= prevTo && t.Duration != null)
         .Select(t => (double)t.Duration!)
         .ToListAsync();
    var prev = prevTracks.Any() ? prevTracks.Average() : 0;

    double? trend = prev > 0 ? (double)(current - prev) / (double)prev : null;
    var goal = prev * 1.1;
    double? goalProgress = goal > 0 ? (double)(current / goal) : null;

    return new MetricDto(current.ToString("N2") + " seconds", trend, goalProgress, goal.ToString("N2") + " seconds");
  }

  [HttpGet("metrics/revenue-per-track")]
  public async Task<MetricDto> GetRevenuePerTrack(DateTime from, DateTime to)
  {
    var revenues = await _context.RevenueEntries
        .Where(r => r.RevenueDate >= from && r.RevenueDate <= to)
        .Select(r => new { r.Amount, r.TrackId })
        .ToListAsync();

    var totalRev = revenues.Sum(r => (double)r.Amount);
    var trackCount = revenues.Where(r => r.TrackId.HasValue).Select(r => r.TrackId).Distinct().Count();
    var current = trackCount > 0 ? totalRev / trackCount : 0;

    var period = to - from;
    var prevFrom = from - period;
    var prevTo = from.AddDays(-1);

    var prevRevenues = await _context.RevenueEntries
        .Where(r => r.RevenueDate >= prevFrom && r.RevenueDate <= prevTo)
        .Select(r => new { r.Amount, r.TrackId })
        .ToListAsync();

    var prevTotalRev = prevRevenues.Sum(r => (double)r.Amount);
    var prevTrackCount = prevRevenues.Where(r => r.TrackId.HasValue).Select(r => r.TrackId).Distinct().Count();
    var prev = prevTrackCount > 0 ? prevTotalRev / prevTrackCount : 0;

    double? trend = prev > 0 ? (double)(current - prev) / (double)prev : null;
    var goal = prev * 1.1;
    double? goalProgress = goal > 0 ? (double)(current / goal) : null;

    return new MetricDto(current.ToString("C2"), trend, goalProgress, goal.ToString("C2"));
  }

  [HttpGet("metrics/top-source-contribution")]
  public async Task<MetricDto> GetTopSourceContribution(DateTime from, DateTime to)
  {
    var data = await _context.RevenueEntries
        .Where(r => r.RevenueDate >= from && r.RevenueDate <= to)
        .GroupBy(r => r.SourceId)
        .Select(g => new { Total = (double)g.Sum(r => r.Amount) })
        .ToListAsync();

    var totalRevenue = data.Sum(x => x.Total);
    var topSrc = data.OrderByDescending(x => x.Total).FirstOrDefault()?.Total ?? 0;
    var current = totalRevenue > 0 ? (topSrc / totalRevenue) * 100 : 0;

    var period = to - from;
    var prevFrom = from - period;
    var prevTo = from.AddDays(-1);

    var prevData = await _context.RevenueEntries
        .Where(r => r.RevenueDate >= prevFrom && r.RevenueDate <= prevTo)
        .GroupBy(r => r.SourceId)
        .Select(g => new { Total = (double)g.Sum(r => r.Amount) })
        .ToListAsync();

    var prevTotalRev = prevData.Sum(x => x.Total);
    var prevTopSrc = prevData.OrderByDescending(x => x.Total).FirstOrDefault()?.Total ?? 0;
    var prev = prevTotalRev > 0 ? (prevTopSrc / prevTotalRev) * 100 : 0;

    double? trend = prev > 0 ? (double)(current - prev) / (double)prev : null;
    var goal = prev * 1.1;
    double? goalProgress = goal > 0 ? (double)(current / goal) : null;

    return new MetricDto(current.ToString("N2") + "%", trend, goalProgress, goal.ToString("N2") + "%");
  }
}
