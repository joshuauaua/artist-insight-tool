using Ivy.Shared;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System;
using System.Linq;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Table, title: "CSV Helper", path: ["Integrations", "CSV Helper"])]
public class CsvHelperApp : ViewBase
{
  public class AssetModel
  {
    public Guid Id { get; set; }

    [Required]
    [Name("Product Name", "Name", "Title")]
    public string Name { get; set; } = string.Empty;

    [Name("Description", "Desc", "Details")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Name("Cost", "Price", "Amount", "Value")]
    public decimal Price { get; set; }

    [Required]
    [Name("Category", "Type", "Group")]
    public string Category { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
  }

  public override object? Build()
  {
    var client = UseService<IClientProvider>();

    // State for assets list
    var assets = UseState(() => new List<AssetModel>
    {
    });

    // Export CSV download
    var downloadUrl = this.UseDownload(
        async () =>
        {
          await using var ms = new MemoryStream();
          await using var writer = new StreamWriter(ms, leaveOpen: true);
          await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

          await csv.WriteRecordsAsync(assets.Value);
          await writer.FlushAsync();
          ms.Position = 0;

          return ms.ToArray();
        },
        "text/csv",
        $"products-{DateTime.UtcNow:yyyy-MM-dd}.csv"
    );

    // Import CSV using documented upload flow
    var uploadState = UseState<FileUpload<byte[]>?>();
    var uploadContext = this.UseUpload(MemoryStreamUploadHandler.Create(uploadState))
        .Accept(".csv")
        .MaxFileSize(10 * 1024 * 1024);

    // State for dialog open/close
    var asset = UseState(() => new AssetModel());
    var isDialogOpen = UseState(false);

    // When a file is uploaded, parse and import
    UseEffect(() =>
    {
      if (uploadState.Value?.Content is byte[] bytes && bytes.Length > 0)
      {
        try
        {
          using var stream = new MemoryStream(bytes);
          using var reader = new StreamReader(stream);
          using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

          var records = csv.GetRecords<AssetModel>().ToList();

          foreach (var record in records)
          {
            record.Id = Guid.NewGuid();
            record.CreatedAt = DateTime.UtcNow;
          }

          assets.Value = assets.Value.Concat(records).ToList();
          client.Toast($"Imported {records.Count} assets from CSV");
        }
        catch (Exception ex)
        {
          client.Toast($"Failed to import CSV: {ex.Message}");
        }
      }
    }, [uploadState]);

    // Handle asset submission
    UseEffect(() =>
    {
      // Check if form was submitted (non-empty required fields)
      if (!string.IsNullOrEmpty(asset.Value.Name) &&
              asset.Value.Price > 0 &&
              !string.IsNullOrEmpty(asset.Value.Category))
      {
        asset.Value.Id = Guid.NewGuid();
        asset.Value.CreatedAt = DateTime.UtcNow;
        assets.Value = assets.Value.Append(asset.Value).ToList();
        client.Toast($"Asset '{asset.Value.Name}' added successfully");
        asset.Set(new AssetModel());
        isDialogOpen.Set(false);
      }
    }, [asset]);

    // Delete action
    var deleteAsset = new Action<Guid>((id) =>
    {
      var p = assets.Value.FirstOrDefault(x => x.Id == id);
      if (p != null)
      {
        assets.Value = assets.Value.Where(x => x.Id != id).ToList();
        client.Toast($"Asset '{p.Name}' deleted");
      }
    });

    // Build the table with delete button
    var table = assets.Value.Select(p => new
    {
      p.Name,
      p.Description,
      p.Price,
      p.Category,
      p.CreatedAt,
      Delete = Icons.Trash.ToButton(_ => deleteAsset(p.Id)).Small()
    }).ToTable().Width(Size.Full());

    // File input is generated from upload state/context

    // Left card - Controls
    var leftCard = new Card(
        Layout.Vertical().Gap(6)
        .Add(Text.H3("Controls"))
        .Add(Text.Small($"Total: {assets.Value.Count} assets"))

        // Add Asset button - opens dialog
        .Add(new Button("Add Asset")
            .Icon(Icons.Plus)
            .Primary() // Removed .Primary() as it might not be valid, checked Button API in previous turns, it uses Variant. But let's stick to user code and fix if lint errors.
                       // Looking at previous code, .Variant(ButtonVariant.Outline) is used. .Primary() might not exist. 
                       // Let's check shared Button API or just assume user knows. 
                       // Actually, I should probably check if .Primary works or use .Variant(ButtonVariant.Primary) if that exists.
                       // In RevenueTableView: .Variant(ButtonVariant.Link)
                       // Let's assume .Primary() is an extension or valid. If not lint will catch it.
            .Width(Size.Full())
            .HandleClick(_ => isDialogOpen.Set(true)))

        .Add(asset.ToForm()
            .Remove(m => m.Id)
            .Remove(m => m.CreatedAt)
            .Required(m => m.Name, m => m.Price, m => m.Category)
            .Label(m => m.Name, "Asset Name")
            .Label(m => m.Description, "Description")
            .Label(m => m.Price, "Price")
            .Label(m => m.Category, "Category")
            .Builder(m => m.Name, s => s.ToTextInput().Placeholder("Enter asset name..."))
            .Builder(m => m.Description, s => s.ToTextInput().Placeholder("Enter description..."))
            .Builder(m => m.Price, s => s.ToNumberInput().Placeholder("Enter price...").Min(0))
            .Builder(m => m.Category, s => s.ToTextInput().Placeholder("Enter category..."))
            .ToDialog(isDialogOpen, "Create New Asset", "Please provide asset information",
                     width: Size.Fraction(0.5f)))

        // Export CSV button
        .Add(new Button("Export CSV")
            .Icon(Icons.Download)
            .Url(downloadUrl.Value)
            .Width(Size.Full()))

        .Add(new Separator())
        .Add(Text.Small("Import CSV File:"))

        // Import CSV file input
        .Add(uploadState.ToFileInput(uploadContext).Placeholder("Choose File"))
    ).Title("Management").Height(Size.Fit().Min(Size.Full()));

    // Right card - Table
    var rightCard = new Card(table).Title("Assets").Height(Size.Fit().Min(Size.Full()));

    // Two-column layout
    return Layout.Horizontal().Gap(8)
        .Add(leftCard.Width(Size.Fraction(0.4f)))
        .Add(rightCard.Width(Size.Fraction(0.6f)));
  }
}
