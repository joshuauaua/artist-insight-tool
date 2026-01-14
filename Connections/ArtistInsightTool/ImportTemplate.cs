using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArtistInsightTool.Connections.ArtistInsightTool;

public partial class ImportTemplate
{
  [Key]
  public int Id { get; set; }

  [Required]
  public string Name { get; set; } = null!;

  [Required]
  public string HeadersJson { get; set; } = "[]";

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }
}
