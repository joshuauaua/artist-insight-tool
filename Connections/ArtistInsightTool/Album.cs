using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArtistInsightTool.Connections.ArtistInsightTool;

[Table("albums")]
[Index("ArtistId", Name = "IX_albums_ArtistId")]
public partial class Album
{
  [Key]
  public int Id { get; set; }

  public int ArtistId { get; set; }

  public string Title { get; set; } = null!;

  public string ReleaseType { get; set; } = "Album";

  public DateTime? ReleaseDate { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  [ForeignKey("ArtistId")]
  [InverseProperty("Albums")]
  public virtual Artist Artist { get; set; } = null!;

  [InverseProperty("Album")]
  public virtual ICollection<RevenueEntry> RevenueEntries { get; set; } = new List<RevenueEntry>();

  [InverseProperty("Album")]
  public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();
}
