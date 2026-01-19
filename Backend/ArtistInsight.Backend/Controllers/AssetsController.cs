using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArtistInsight.Backend.Data;
using ArtistInsight.Backend.Models;

namespace ArtistInsight.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetsController : ControllerBase
{
  private readonly ArtistInsightContext _context;

  public AssetsController(ArtistInsightContext context)
  {
    _context = context;
  }

  [HttpGet]
  public async Task<ActionResult<IEnumerable<Asset>>> GetAssets()
  {
    var assets = await _context.Assets
        .Include(a => a.AssetRevenues)
        .Select(a => new Asset
        {
          Id = a.Id,
          Name = a.Name,
          Type = a.Type,
          Category = a.Category,
          Collection = a.Collection,
          AmountGenerated = a.AssetRevenues.Sum(ar => ar.Amount)
        })
        .ToListAsync();

    return assets;
  }

  [HttpGet("{id}")]
  public async Task<ActionResult<Asset>> GetAsset(int id)
  {
    var asset = await _context.Assets.FindAsync(id);

    if (asset == null)
    {
      return NotFound();
    }

    return asset;
  }

  [HttpPost]
  public async Task<ActionResult<Asset>> PostAsset(Asset asset)
  {
    _context.Assets.Add(asset);
    await _context.SaveChangesAsync();

    return CreatedAtAction(nameof(GetAsset), new { id = asset.Id }, asset);
  }

  [HttpDelete("{id}")]
  public async Task<IActionResult> DeleteAsset(int id)
  {
    var asset = await _context.Assets.FindAsync(id);
    if (asset == null)
    {
      return NotFound();
    }

    _context.Assets.Remove(asset);
    await _context.SaveChangesAsync();

    return NoContent();
  }
}
