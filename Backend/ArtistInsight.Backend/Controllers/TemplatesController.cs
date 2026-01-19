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

  [HttpDelete("{id}")]
  public async Task<IActionResult> DeleteTemplate(int id)
  {
    var template = await _context.ImportTemplates.FindAsync(id);
    if (template == null)
    {
      return NotFound();
    }

    _context.ImportTemplates.Remove(template);
    await _context.SaveChangesAsync();

    return NoContent();
  }
}
