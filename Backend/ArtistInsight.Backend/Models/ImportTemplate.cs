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

  [Required]
  public string HeadersJson { get; set; } = "[]";

  public string Category { get; set; } = "Other";

  public string? AssetColumn { get; set; }

  public string? AmountColumn { get; set; }

  public string? TerritoryColumn { get; set; }
  public string? LabelColumn { get; set; }
  public string? CollectionColumn { get; set; }
  public string? ArtistColumn { get; set; }
  public string? StoreColumn { get; set; }
  public string? DspColumn { get; set; }
  public string? GrossColumn { get; set; }
  public string? NetColumn { get; set; }
  public string? CurrencyColumn { get; set; }
  public string? AssetTypeColumn { get; set; }

  public List<string> GetHeaders()
  {
    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(HeadersJson) ?? [];
  }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  [InverseProperty("ImportTemplate")]
  public virtual ICollection<RevenueEntry> RevenueEntries { get; set; } = new List<RevenueEntry>();
}
