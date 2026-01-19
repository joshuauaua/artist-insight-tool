using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArtistInsight.Backend.Models;

[Table("artists")]
public partial class Artist
{
  [Key]
  public int Id { get; set; }

  public string Name { get; set; } = null!;

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  [InverseProperty("Artist")]
  public virtual ICollection<Album> Albums { get; set; } = new List<Album>();

  [InverseProperty("Artist")]
  public virtual ICollection<RevenueEntry> RevenueEntries { get; set; } = new List<RevenueEntry>();

  [InverseProperty("Artist")]
  public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();
}
