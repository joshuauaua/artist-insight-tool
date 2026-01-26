using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static ExcelDataReaderExample.ExcelDataReaderSheet;

namespace ExcelDataReaderExample;

public class ImportConfirmationSheet(List<CurrentFile> files, Action onSuccess, Action onCancel) : ViewBase
{
  private readonly List<CurrentFile> _files = files;
  private readonly Action _onSuccess = onSuccess;
  private readonly Action _onCancel = onCancel;

  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();

    // --- Upload/Save State ---
    var uploadName = UseState("");

    // --- Smart Naming State (Royalties) ---
    var uploadYear = UseState(DateTime.Now.Year);
    var uploadQuarter = UseState("Q1");

    // Auto-name file based on Royalties selection
    UseEffect(() =>
    {
      var firstT = _files.FirstOrDefault()?.MatchedTemplate;
      if (firstT?.Category == "Royalties")
      {
        uploadName.Set($"{uploadYear.Value} {uploadQuarter.Value} {firstT.Name}");
      }
    }, [uploadYear, uploadQuarter]);

    if (_files.Count == 0) return Text.Muted("No valid files to import.");

    return Layout.Vertical().Gap(20).Width(Size.Full())
         .Add(Layout.Horizontal().Gap(10).Align(Align.Center)
             .Add(new Button("Back", () => _onCancel()).Variant(ButtonVariant.Link).Icon(Icons.ArrowLeft))
             .Add(Text.H4("Confirm Import")))
         .Add(Text.Label($"Ready to import {_files.Count} files."))
         .Add(Layout.Vertical().Gap(5).Add(_files.Select(f => Text.Muted($"• {f.OriginalName} ({f.MatchedTemplate?.Name})")).ToArray()))
         .Add(Layout.Vertical().Gap(10)
             .Add(Text.Label("Batch Name (Revenue Entry Description)"))
             .Add(uploadName.ToTextInput().Placeholder("e.g. 2024 Q1 Royalties")))
         .Add(Layout.Vertical().Gap(10)
             .Add("Smart Naming (Royalties)")
             .Add(Layout.Horizontal().Gap(10)
                 .Add(uploadYear.ToSelectInput(Enumerable.Range(2020, 10).Select(y => new Option<int>(y.ToString(), y))).Width(100))
                 .Add(uploadQuarter.ToSelectInput(new[] { "Q1", "Q2", "Q3", "Q4" }.Select(q => new Option<string>(q, q))).Width(100))))
         .Add(new Button("Import Data", async () =>
         {
           if (string.IsNullOrWhiteSpace(uploadName.Value)) { client.Toast("Name required", "Warning"); return; }

           await using var db = factory.CreateDbContext();

           foreach (var file in _files)
           {
             if (file.Path == null || file.MatchedTemplate == null) continue;
             var jsonData = ExcelDataReaderSheet.ParseData(file.Path, file.MatchedTemplate);

             // Try to resolve source
             RevenueSource? source = await db.RevenueSources.FirstOrDefaultAsync(s => s.DescriptionText == "Excel Import");
             if (source == null)
             {
               source = new RevenueSource { DescriptionText = "Excel Import" };
               db.RevenueSources.Add(source);
               await db.SaveChangesAsync();
             }

             // Ensure at least one artist exists
             var artist = await db.Artists.FirstOrDefaultAsync();
             if (artist == null)
             {
               artist = new Artist { Name = "Unknown Artist", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
               db.Artists.Add(artist);
               await db.SaveChangesAsync();
             }

             var sheetData = new
             {
               Title = "Main Data",
               FileName = file.OriginalName,
               TemplateName = file.MatchedTemplate?.Name ?? "Unknown",
               Rows = jsonData
             };

             var entry = new RevenueEntry
             {
               RevenueDate = DateTime.Now,
               Description = $"{uploadName.Value} - {file.OriginalName}",
               Amount = 0,
               SourceId = source.Id,
               ArtistId = artist.Id,
               CreatedAt = DateTime.UtcNow,
               UpdatedAt = DateTime.UtcNow,
               JsonData = JsonSerializer.Serialize(new[] { sheetData })
             };

             var tmpl = file.MatchedTemplate!;
             decimal totalAmount = 0;
             if (!string.IsNullOrEmpty(tmpl.AmountColumn))
             {
               foreach (var row in jsonData)
               {
                 if (row.TryGetValue(tmpl.AmountColumn, out var val) && decimal.TryParse(val?.ToString(), out var d))
                   totalAmount += d;
               }
             }
             entry.Amount = totalAmount;

             if (tmpl.Category == "Royalties")
             {
               entry.Year = uploadYear.Value;
               entry.Quarter = uploadQuarter.Value;
             }
             entry.ImportTemplateId = tmpl.Id;
             entry.FileName = file.OriginalName;

             db.RevenueEntries.Add(entry);
             await db.SaveChangesAsync(); // Save to get ID

             // Asset Logic
             if (!string.IsNullOrEmpty(tmpl.AssetColumn))
             {
               try
               {
                 var assetCol = tmpl.AssetColumn;
                 var amountCol = tmpl.AmountColumn; // Net
                 var collectionCol = tmpl.CollectionColumn;
                 var batchAssets = new Dictionary<string, decimal>();
                 var assetCollections = new Dictionary<string, string>();

                 foreach (var row in jsonData)
                 {
                   if (row.TryGetValue(assetCol, out var nameObj) && row.TryGetValue(amountCol, out var amountObj))
                   {
                     var name = nameObj?.ToString()?.Trim();
                     if (!string.IsNullOrEmpty(name) && decimal.TryParse(amountObj?.ToString(), out var amount))
                     {
                       if (!batchAssets.ContainsKey(name))
                       {
                         batchAssets[name] = 0;
                         if (!string.IsNullOrEmpty(collectionCol) && row.TryGetValue(collectionCol, out var colObj))
                         {
                           var colStr = colObj?.ToString()?.Trim();
                           if (!string.IsNullOrEmpty(colStr)) assetCollections[name] = colStr;
                         }
                       }
                       batchAssets[name] += amount;
                     }
                   }
                 }

                 if (batchAssets.Count > 0)
                 {
                   var names = batchAssets.Keys.ToList();
                   var existingAssets = await db.Assets.Where(a => names.Contains(a.Name)).ToListAsync();
                   var existingNames = existingAssets.Select(a => a.Name).ToHashSet();
                   var newAssets = names.Where(n => !existingNames.Contains(n))
                                            .Select(n =>
                                            {
                                              var type = "";
                                              if (!string.IsNullOrEmpty(tmpl.AssetTypeColumn))
                                              {
                                                var rowWithAsset = jsonData.FirstOrDefault(r => r.GetValueOrDefault(assetCol)?.ToString()?.Trim() == n);
                                                if (rowWithAsset != null && rowWithAsset.TryGetValue(tmpl.AssetTypeColumn, out var typeObj))
                                                {
                                                  type = typeObj?.ToString()?.Trim() ?? "";
                                                }
                                              }

                                              if (string.IsNullOrEmpty(type))
                                              {
                                                type = tmpl.Category switch
                                                {
                                                  "Royalties" => "Single",
                                                  "Merchandise" => "Single Item",
                                                  "Concerts" => "Ticket Sales",
                                                  _ => "Default"
                                                };
                                              }

                                              return new Asset
                                              {
                                                Name = n,
                                                Type = type,
                                                Category = tmpl.Category,
                                                Collection = assetCollections.GetValueOrDefault(n) ?? "",
                                                AmountGenerated = 0
                                              };
                                            })
                                            .ToList();

                   if (newAssets.Count > 0)
                   {
                     db.Assets.AddRange(newAssets);
                     await db.SaveChangesAsync();
                     existingAssets.AddRange(newAssets);
                   }

                   var assetMap = existingAssets.ToDictionary(a => a.Name, a => a.Id);
                   var revenueRecords = batchAssets.Select(kvp => new AssetRevenue
                   {
                     AssetId = assetMap[kvp.Key],
                     RevenueEntry = entry,
                     Amount = kvp.Value
                   });
                   db.AssetRevenues.AddRange(revenueRecords);
                 }
               }
               catch { }
             }
           }

           await db.SaveChangesAsync();
           client.Toast($"Imported {_files.Count} files", "Success");
           _onSuccess();
         }).Variant(ButtonVariant.Primary).Width(Size.Full()));
  }
}
