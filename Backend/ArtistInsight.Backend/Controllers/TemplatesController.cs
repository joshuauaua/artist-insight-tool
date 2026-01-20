using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArtistInsight.Backend.Data;
using ArtistInsight.Backend.Models;

namespace ArtistInsight.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TemplatesController : ControllerBase
{
  private readonly ArtistInsightContext _context;

  public TemplatesController(ArtistInsightContext context)
  {
    _context = context;
  }

  [HttpGet]
  public async Task<ActionResult<IEnumerable<ImportTemplate>>> GetTemplates()
  {
    return await _context.ImportTemplates
        .Include(t => t.RevenueEntries)
        .ToListAsync();
  }

  [HttpGet("{id}")]
  public async Task<ActionResult<ImportTemplate>> GetTemplate(int id)
  {
    var template = await _context.ImportTemplates.FindAsync(id);

    if (template == null)
    {
      return NotFound();
    }

    return template;
  }

  [HttpPost]
  public async Task<ActionResult<ImportTemplate>> PostTemplate(ImportTemplate template)
  {
    _context.ImportTemplates.Add(template);
    await _context.SaveChangesAsync();

    return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
  }

  [HttpPut("{id}")]
  public async Task<IActionResult> PutTemplate(int id, ImportTemplate template)
  {
    if (id != template.Id)
    {
      return BadRequest();
    }

    _context.Entry(template).State = EntityState.Modified;

    try
    {
      await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
      if (!_context.ImportTemplates.Any(e => e.Id == id))
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
  public async Task<IActionResult> DeleteTemplate(int id)
  {
    var template = await _context.ImportTemplates.FindAsync(id);
    if (template == null)
    {
      return NotFound();
    }

    var linkedEntries = await _context.RevenueEntries.Where(e => e.ImportTemplateId == id).ToListAsync();
    foreach (var entry in linkedEntries)
    {
      entry.ImportTemplateId = null;
    }
    await _context.SaveChangesAsync();

    _context.ImportTemplates.Remove(template);
    await _context.SaveChangesAsync();

    return NoContent();
  }
}
