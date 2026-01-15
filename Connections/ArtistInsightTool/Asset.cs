using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArtistInsightTool.Connections.ArtistInsightTool;

[Table("assets")]
public class Asset
{
  [Key]
  public int Id { get; set; }

  [Required]
  [MaxLength(100)]
  public string Name { get; set; } = "";

  [MaxLength(50)]
  public string Type { get; set; } = "";

  [Column(TypeName = "decimal(18,2)")]
  public decimal AmountGenerated { get; set; }
}
