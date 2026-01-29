using System.ComponentModel.DataAnnotations;

namespace ArtistInsight.Backend.Models;

public class DashboardSetting
{
    [Key]
    public string Key { get; set; } = "";
    public string Value { get; set; } = ""; // JSON blob of the settings
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
