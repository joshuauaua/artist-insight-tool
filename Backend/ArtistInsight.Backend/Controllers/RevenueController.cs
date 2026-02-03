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
            r.ImportTemplate != null ? r.ImportTemplate.Category : "Other",
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

    // 1. Identify Assets currently linked to this upload
    var affectedAssetIds = await _context.AssetRevenues
        .Where(ar => ar.RevenueEntryId == id)
        .Select(ar => ar.AssetId)
        .Distinct()
        .ToListAsync();

    // 2. Remove the Revenue Entry (and assume Cascade or Manual delete of links)
    // To be safe and ensure state consistency for the check below, we remove links explicitly.
    var links = _context.AssetRevenues.Where(ar => ar.RevenueEntryId == id);
    _context.AssetRevenues.RemoveRange(links);
    _context.RevenueEntries.Remove(revenueEntry);

    // Commit the deletion of the entry and its links so the next check is accurate against DB
    await _context.SaveChangesAsync();

    // 3. Check for Orphaned Assets
    foreach (var assetId in affectedAssetIds)
    {
      bool hasRemainingLinks = await _context.AssetRevenues.AnyAsync(ar => ar.AssetId == assetId);
      if (!hasRemainingLinks)
      {
        var orphanAsset = await _context.Assets.FindAsync(assetId);
        if (orphanAsset != null)
        {
          _context.Assets.Remove(orphanAsset);
        }
      }
    }

    await _context.SaveChangesAsync();

    return NoContent();
  }
}
