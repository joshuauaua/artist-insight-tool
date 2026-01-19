using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArtistInsightTool.Connections.ArtistInsightTool;

[Table("tracks")]

[Index("ArtistId", Name = "IX_tracks_ArtistId")]
public partial class Track
{
  [Key]
  public int Id { get; set; }

  public int ArtistId { get; set; }

  public string Title { get; set; } = null!;

  public int? Duration { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  [ForeignKey("ArtistId")]
  [InverseProperty("Tracks")]
  public virtual Artist Artist { get; set; } = null!;

  [InverseProperty("Track")]
  public virtual ICollection<RevenueEntry> RevenueEntries { get; set; } = new List<RevenueEntry>();
}
