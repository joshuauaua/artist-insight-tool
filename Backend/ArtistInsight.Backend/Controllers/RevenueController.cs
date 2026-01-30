using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArtistInsight.Backend.Data;
using ArtistInsight.Backend.Models;

namespace ArtistInsight.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RevenueController : ControllerBase
{
  private readonly ArtistInsightContext _context;

  public RevenueController(ArtistInsightContext context)
  {
    _context = context;
  }

  [HttpGet("find")]
  public async Task<ActionResult<RevenueEntry>> FindRevenueEntry(
      [FromQuery] int year,
      [FromQuery] string quarter,
      [FromQuery] int sourceId)
  {
    var entry = await _context.RevenueEntries
        .Include(r => r.Artist)
        .Include(r => r.Source)
        .Include(r => r.ImportTemplate)
        .FirstOrDefaultAsync(r => r.SourceId == sourceId && r.Year == year && r.Quarter == quarter);

    if (entry == null)
    {
      return NotFound();
    }

    return entry;
  }

  [HttpGet("entries")]
  public async Task<ActionResult<IEnumerable<RevenueEntryDto>>> GetRevenueEntries()
  {
    return await _context.RevenueEntries
        .Include(r => r.Artist)
        .Include(r => r.Source)

        .Include(r => r.ImportTemplate)
        .OrderByDescending(r => r.RevenueDate)
        .Select(r => new RevenueEntryDto(
            r.Id,
            r.RevenueDate,
            r.Amount,
            r.Description,
            r.Source.DescriptionText,
            r.Integration,

            r.Artist.Name,
            r.ImportTemplate != null ? r.ImportTemplate.Name : null,
            r.JsonData,
            r.UploadDate ?? r.CreatedAt,
            r.Year,
            r.Quarter
        ))
        .ToListAsync();
  }

  [HttpGet("{id}")]
  public async Task<ActionResult<RevenueEntry>> GetRevenueEntry(int id)
  {
    var revenueEntry = await _context.RevenueEntries
        .Include(r => r.Artist)
        .Include(r => r.Source)

        .Include(r => r.ImportTemplate)
        .FirstOrDefaultAsync(r => r.Id == id);

    if (revenueEntry == null)
    {
      return NotFound();
    }

    return revenueEntry;
  }

  [HttpPost]
  public async Task<ActionResult<RevenueEntry>> PostRevenueEntry(RevenueEntry revenueEntry)
  {
    _context.RevenueEntries.Add(revenueEntry);
    await _context.SaveChangesAsync();

    return CreatedAtAction(nameof(GetRevenueEntry), new { id = revenueEntry.Id }, revenueEntry);
  }

  [HttpPut("{id}")]
  public async Task<IActionResult> PutRevenueEntry(int id, RevenueEntry revenueEntry)
  {
    if (id != revenueEntry.Id)
    {
      return BadRequest();
    }

    _context.Entry(revenueEntry).State = EntityState.Modified;

    try
    {
      await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
      if (!_context.RevenueEntries.Any(e => e.Id == id))
      {
        return NotFound();
      }
      else
      {
        throw;
      }
    }

    return NoContent();
  }

  [HttpDelete("{id}")]
  public async Task<IActionResult> DeleteRevenueEntry(int id)
  {
    var revenueEntry = await _context.RevenueEntries.FindAsync(id);
    if (revenueEntry == null)
    {
      return NotFound();
    }

    _context.RevenueEntries.Remove(revenueEntry);
    await _context.SaveChangesAsync();

    return NoContent();
  }
}
