using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static ExcelDataReaderExample.ExcelDataReaderSheet;
using ArtistInsightTool.Apps.Services;

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
    var queryService = UseService<IQueryService>();
    var service = UseService<ArtistInsightService>();

    // --- Upload/Save State ---
    var uploadName = UseState("");

    // --- Smart Naming State ---
    var uploadYear = UseState(DateTime.Now.Year);
    var uploadQuarter = UseState("Q1");
    var errorState = UseState<string?>(() => null);

    // Auto-name file based on selection
    UseEffect(() =>
    {
      var firstT = _files.FirstOrDefault()?.MatchedTemplate;
      if (firstT != null)
      {
        uploadName.Set($"{uploadYear.Value} {uploadQuarter.Value} {firstT.Name}");
      }
    }, [uploadYear, uploadQuarter]);

    if (_files.Count == 0) return Text.Muted("No valid files to import.");

    return Layout.Vertical().Gap(10).Width(Size.Full())
         .Add(Layout.Vertical().Gap(2)
             .Add(Text.Label($"{_files.Count} Files Selected").Muted().Small())
             .Add(Layout.Vertical().Gap(2).Add(_files.Select(f => Text.Muted($"• {f.OriginalName} ({f.MatchedTemplate?.Name})").Small()).ToArray())))
         .Add(Layout.Vertical().Gap(4)
             .Add(Text.Label("Select Timeframe").Small())
             .Add(Layout.Horizontal().Gap(10)
                 .Add(uploadYear.ToSelectInput(Enumerable.Range(2020, DateTime.Now.Year - 2020 + 1).Select(y => new Option<int>(y.ToString(), y))).Width(100))
                 .Add(uploadQuarter.ToSelectInput(new[] { "Q1", "Q2", "Q3", "Q4" }.Select(q => new Option<string>(q, q))).Width(100))))
         .Add(Layout.Vertical().Gap(4)
             .Add(Text.Label("Entry Name").Small())
             .Add(uploadName.ToTextInput().Placeholder("e.g. 2024 Q1 Royalties")))
         .Add(new Spacer().Height(5))
         .Add(new Button("Import Data", async () =>
         {
           if (string.IsNullOrWhiteSpace(uploadName.Value)) { client.Toast("Name required", "Warning"); return; }

           await using var db = factory.CreateDbContext();

           int filesImported = 0;
           foreach (var file in _files)
           {
             try
             {
               if (file.Path == null || file.MatchedTemplate == null) continue;
               var jsonData = ExcelDataReaderSheet.ParseData(file.Path, file.MatchedTemplate);

               // Try to resolve source from Template
               string sourceName = !string.IsNullOrWhiteSpace(file.MatchedTemplate?.SourceName) ? file.MatchedTemplate.SourceName : "Excel Import";
               RevenueSource? source = await db.RevenueSources.FirstOrDefaultAsync(s => s.DescriptionText == sourceName);
               if (source == null)
               {
                 source = new RevenueSource { DescriptionText = sourceName };
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

               // Check if this specific file was already imported for this timeframe/source
               var existingFile = await db.RevenueEntries.FirstOrDefaultAsync(e =>
                    e.FileName == file.OriginalName &&
                    e.Year == uploadYear.Value &&
                    e.Quarter == uploadQuarter.Value &&
                    e.SourceId == source.Id);

               if (existingFile != null)
               {
                 client.Toast($"File '{file.OriginalName}' was already imported for this period.", "Warning");
                 continue;
               }

               var sheetData = new Dictionary<string, object?>
               {
                 ["Title"] = file.OriginalName,
                 ["FileName"] = file.OriginalName,
                 ["TemplateName"] = file.MatchedTemplate?.Name ?? "Unknown",
                 ["Rows"] = jsonData
               };

               var tmpl = file.MatchedTemplate!;
               var mappings = tmpl.GetMappings();
               string? GetHeader(string systemField) => mappings.FirstOrDefault(x => x.Value == systemField).Key;

               var amountCol = GetHeader("Net");
               var assetCol = GetHeader("AssetTitle");

               decimal totalAmount = 0;
               if (!string.IsNullOrEmpty(amountCol))
               {
                 foreach (var row in jsonData)
                 {
                   if (row.TryGetValue(amountCol, out var val) && decimal.TryParse(val?.ToString(), out var d))
                     totalAmount += d;
                 }
               }

               var entry = new RevenueEntry
               {
                 RevenueDate = DateTime.Now,
                 Description = file.OriginalName,
                 Amount = totalAmount,
                 SourceId = source.Id,
                 ArtistId = artist.Id,
                 CreatedAt = DateTime.UtcNow,
                 UpdatedAt = DateTime.UtcNow,
                 JsonData = JsonSerializer.Serialize(new[] { sheetData }),
                 Year = uploadYear.Value,
                 Quarter = uploadQuarter.Value,
                 ImportTemplateId = tmpl.Id,
                 FileName = file.OriginalName
               };

               var result = await service.CreateRevenueEntryAsync(entry);
               if (result != null)
               {
                 entry.Id = result.Id;
                 filesImported++;

                 if (!string.IsNullOrEmpty(assetCol))
                 {
                   try
                   {
                     var batchAssets = new Dictionary<string, decimal>();
                     foreach (var row in jsonData)
                     {
                       if (row.TryGetValue(assetCol, out var nameObj))
                       {
                         var name = nameObj?.ToString()?.Trim();
                         if (string.IsNullOrEmpty(name)) continue;
                         decimal amount = 0;
                         if (!string.IsNullOrEmpty(amountCol) && row.TryGetValue(amountCol, out var amountObj))
                           decimal.TryParse(amountObj?.ToString(), out amount);
                         if (!batchAssets.ContainsKey(name)) batchAssets[name] = 0;
                         batchAssets[name] += amount;
                       }
                     }
                     if (batchAssets.Count > 0)
                     {
                       var names = batchAssets.Keys.ToList();
                       var existingAssets = await db.Assets.Where(a => names.Contains(a.Name)).ToListAsync();
                       var existingNames = existingAssets.Select(a => a.Name).ToHashSet();
                       var newAssets = names.Where(n => !existingNames.Contains(n))
                                               .Select(n => new Asset { Name = n, Type = "Default", Category = tmpl.Category, Collection = "", AmountGenerated = 0 })
                                               .ToList();
                       if (newAssets.Count > 0)
                       {
                         db.Assets.AddRange(newAssets);
                         await db.SaveChangesAsync();
                         existingAssets.AddRange(newAssets);
                       }
                       var assetMap = existingAssets.ToDictionary(a => a.Name, a => a.Id);
                       var revenueRecords = batchAssets.Select(kvp => new AssetRevenue { AssetId = assetMap[kvp.Key], RevenueEntryId = entry.Id, Amount = kvp.Value });
                       db.AssetRevenues.AddRange(revenueRecords);
                       await db.SaveChangesAsync();
                     }
                   }
                   catch { }
                 }
               }
               else client.Toast($"Failed to create entry for {file.OriginalName}", "Error");
             }
             catch (Exception ex) { client.Toast($"Error processing {file.OriginalName}: {ex.Message}", "Error"); }
           }

           if (filesImported > 0)
           {
             client.Toast($"Imported {filesImported} files", "Success");
             queryService.RevalidateByTag("revenue_entries");
             queryService.RevalidateByTag("assets");
             queryService.RevalidateByTag("dashboard_total_revenue");
             queryService.RevalidateByTag("dashboard_targeted_revenue");
             queryService.RevalidateByTag("uploads_list");
             queryService.RevalidateByTag("templates_list");
             _onSuccess();
           }
         }).Variant(ButtonVariant.Primary).Width(Size.Full()).WithConfetti(AnimationTrigger.Click))
         .Add(errorState.Value != null ? new Dialog(
             _ => errorState.Set(_ => null),
             new DialogHeader("Import Warning"),
             new DialogBody(Text.Label(errorState.Value).Color(Colors.Red)),
             new DialogFooter(new Button("OK", () => errorState.Set(_ => null)).Variant(ButtonVariant.Primary))
         ) : null);
  }
}
