using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArtistInsight.Backend.Models;

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

  [MaxLength(50)]
  public string Category { get; set; } = "";

  [MaxLength(100)]
  public string Collection { get; set; } = "";

  [Column(TypeName = "decimal(18,2)")]
  public decimal AmountGenerated { get; set; }

  [Column(TypeName = "decimal(18,2)")]
  public decimal GrossAmount { get; set; }

  [Column(TypeName = "decimal(18,2)")]
  public decimal NetAmount { get; set; }

  public virtual ICollection<AssetRevenue> AssetRevenues { get; set; } = new List<AssetRevenue>();
}
