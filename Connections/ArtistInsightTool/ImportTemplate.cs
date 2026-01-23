using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArtistInsightTool.Connections.ArtistInsightTool;

[Table("import_templates")]
public partial class ImportTemplate
{
  [Key]
  public int Id { get; set; }

  [Required]
  public string Name { get; set; } = null!;

  [Required]
  public string HeadersJson { get; set; } = "[]";

  public string Category { get; set; } = "Other";

  public string? AssetColumn { get; set; }
  public string? AmountColumn { get; set; }
  public string? TransactionDateColumn { get; set; }
  public string? TransactionIdColumn { get; set; }
  public string? SourcePlatformColumn { get; set; }
  public string? CategoryColumn { get; set; }
  public string? QuantityColumn { get; set; }

  public string? TerritoryColumn { get; set; }
  public string? LabelColumn { get; set; }
  public string? CollectionColumn { get; set; }
  public string? ArtistColumn { get; set; }
  public string? StoreColumn { get; set; }
  public string? DspColumn { get; set; }
  public string? GrossColumn { get; set; }
  public string? NetColumn { get; set; }
  public string? CurrencyColumn { get; set; }

  // Category Specific
  public string? SkuColumn { get; set; }
  public string? CustomerEmailColumn { get; set; }
  public string? IsrcColumn { get; set; }
  public string? UpcColumn { get; set; }
  public string? VenueNameColumn { get; set; }
  public string? EventStatusColumn { get; set; }
  public string? TicketClassColumn { get; set; }

  public List<string> GetHeaders()
  {
    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(HeadersJson) ?? [];
  }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  [InverseProperty("ImportTemplate")]
  public virtual ICollection<RevenueEntry> RevenueEntries { get; set; } = new List<RevenueEntry>();
}
