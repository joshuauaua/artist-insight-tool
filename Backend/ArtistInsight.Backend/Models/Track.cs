using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArtistInsight.Backend.Models;

[Table("tracks")]
public partial class Track
{
  [Key]
  public int Id { get; set; }

  public int ArtistId { get; set; }

  public int? AlbumId { get; set; }

  public string Title { get; set; } = null!;

  public int? Duration { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  [ForeignKey("AlbumId")]
  [InverseProperty("Tracks")]
  public virtual Album? Album { get; set; }

  [ForeignKey("ArtistId")]
  [InverseProperty("Tracks")]
  public virtual Artist Artist { get; set; } = null!;

  [InverseProperty("Track")]
  public virtual ICollection<RevenueEntry> RevenueEntries { get; set; } = new List<RevenueEntry>();
}
