using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArtistInsightTool.Connections.ArtistInsightTool;

[Table("asset_revenues")]
public class AssetRevenue
{
  [Key]
  public int Id { get; set; }

  public int AssetId { get; set; }

  public int RevenueEntryId { get; set; }

  [Column(TypeName = "decimal(18,2)")]
  public decimal Amount { get; set; }

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  [ForeignKey("AssetId")]
  public virtual Asset Asset { get; set; } = null!;

  [ForeignKey("RevenueEntryId")]
  public virtual RevenueEntry RevenueEntry { get; set; } = null!;
}
