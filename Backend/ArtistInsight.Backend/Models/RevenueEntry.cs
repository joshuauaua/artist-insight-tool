using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArtistInsight.Backend.Models;

[Table("revenue_entries")]
public partial class RevenueEntry
{
  [Key]
  public int Id { get; set; }

  public int ArtistId { get; set; }

  public int SourceId { get; set; }

  [MaxLength(50)]
  public string? Integration { get; set; }

  [Column(TypeName = "decimal(18,2)")]
  public decimal Amount { get; set; }

  public DateTime RevenueDate { get; set; }

  public string? Description { get; set; }

  public string? JsonData { get; set; }

  public DateTime? UploadDate { get; set; }
  public int? Year { get; set; }
  public string? Quarter { get; set; }
  public string? FileName { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  [ForeignKey("ArtistId")]
  [InverseProperty("RevenueEntries")]
  [System.Text.Json.Serialization.JsonIgnore]
  public virtual Artist? Artist { get; set; }

  [ForeignKey("SourceId")]
  [InverseProperty("RevenueEntries")]
  [System.Text.Json.Serialization.JsonIgnore]
  public virtual RevenueSource? Source { get; set; }

  public virtual ICollection<AssetRevenue> AssetRevenues { get; set; } = new List<AssetRevenue>();

  public string? ColumnMapping { get; set; }

  public int? ImportTemplateId { get; set; }

  [ForeignKey("ImportTemplateId")]
  public virtual ImportTemplate? ImportTemplate { get; set; }
}
