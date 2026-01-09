using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArtistInsightTool.Connections.ArtistInsightTool;

[Table("campaigns")]
[Index("ArtistId", Name = "IX_campaigns_ArtistId")]
public partial class Campaign
{
    [Key]
    public int Id { get; set; }

    public int ArtistId { get; set; }

    public string Name { get; set; } = null!;

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [ForeignKey("ArtistId")]
    [InverseProperty("Campaigns")]
    public virtual Artist Artist { get; set; } = null!;

    [InverseProperty("Campaign")]
    public virtual ICollection<RevenueEntry> RevenueEntries { get; set; } = new List<RevenueEntry>();
}
