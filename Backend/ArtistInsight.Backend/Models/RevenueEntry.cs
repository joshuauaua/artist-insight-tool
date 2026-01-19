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

  public int? TrackId { get; set; }

  public int? AlbumId { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  [ForeignKey("AlbumId")]
  [InverseProperty("RevenueEntries")]
  public virtual Album? Album { get; set; }

  [ForeignKey("ArtistId")]
  [InverseProperty("RevenueEntries")]
  public virtual Artist Artist { get; set; } = null!;

  [ForeignKey("SourceId")]
  [InverseProperty("RevenueEntries")]
  public virtual RevenueSource Source { get; set; } = null!;

  [ForeignKey("TrackId")]
  [InverseProperty("RevenueEntries")]
  public virtual Track? Track { get; set; }

  public virtual ICollection<AssetRevenue> AssetRevenues { get; set; } = new List<AssetRevenue>();

  public string? ColumnMapping { get; set; }

  public int? ImportTemplateId { get; set; }

  [ForeignKey("ImportTemplateId")]
  public virtual ImportTemplate? ImportTemplate { get; set; }
}
