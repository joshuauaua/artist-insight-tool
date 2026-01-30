using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArtistInsightTool.Connections.ArtistInsightTool;

[Table("revenue_entries")]
[Index("ArtistId", Name = "IX_revenue_entries_ArtistId")]
[Index("SourceId", Name = "IX_revenue_entries_SourceId")]
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
  public virtual Artist? Artist { get; set; }

  [ForeignKey("SourceId")]
  [InverseProperty("RevenueEntries")]
  public virtual RevenueSource? Source { get; set; }

  public virtual ICollection<AssetRevenue> AssetRevenues { get; set; } = new List<AssetRevenue>();

  public string? ColumnMapping { get; set; }

  public int? ImportTemplateId { get; set; }
  public virtual ImportTemplate? ImportTemplate { get; set; }
}
