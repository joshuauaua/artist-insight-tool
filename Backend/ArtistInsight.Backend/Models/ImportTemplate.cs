using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArtistInsight.Backend.Models;

[Table("import_templates")]
public partial class ImportTemplate
{
  [Key]
  public int Id { get; set; }

  [Required]
  public string Name { get; set; } = null!;

  public string? SourceName { get; set; }

  [Required]
  public string HeadersJson { get; set; } = "[]";

  public string Category { get; set; } = "Other";

  public string? MappingsJson { get; set; } = "{}";

  public List<string> GetHeaders()
  {
    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(HeadersJson) ?? [];
  }

  public Dictionary<string, string> GetMappings()
  {
    return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(MappingsJson ?? "{}") ?? [];
  }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  [InverseProperty("ImportTemplate")]
  public virtual ICollection<RevenueEntry> RevenueEntries { get; set; } = new List<RevenueEntry>();
}
