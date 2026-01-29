using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArtistInsight.Backend.Data;
using ArtistInsight.Backend.Models;

namespace ArtistInsight.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ArtistInsightContext _context;

    public SettingsController(ArtistInsightContext context)
    {
        _context = context;
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<DashboardSetting>> GetSetting(string key)
    {
        var setting = await _context.DashboardSettings.FindAsync(key);
        if (setting == null) return NotFound();
        return setting;
    }

    [HttpPost]
    public async Task<ActionResult<DashboardSetting>> SaveSetting(DashboardSetting setting)
    {
        var existing = await _context.DashboardSettings.FindAsync(setting.Key);
        if (existing != null)
        {
            existing.Value = setting.Value;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.Entry(existing).State = EntityState.Modified;
        }
        else
        {
            setting.UpdatedAt = DateTime.UtcNow;
            _context.DashboardSettings.Add(setting);
        }

        await _context.SaveChangesAsync();
        return Ok(setting);
    }
}
