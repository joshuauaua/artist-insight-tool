using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArtistInsight.Backend.Models;

[Table("revenue_sources")]
public partial class RevenueSource
{
  [Key]
  public int Id { get; set; }

  public string DescriptionText { get; set; } = null!;

  [InverseProperty("Source")]
  public virtual ICollection<RevenueEntry> RevenueEntries { get; set; } = new List<RevenueEntry>();
}
