using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static ExcelDataReaderExample.ExcelDataReaderSheet;

namespace ExcelDataReaderExample;

public class AnnexSheet(CurrentFile? file, Action onClose) : ViewBase
{
  private readonly CurrentFile? _file = file;
  private readonly Action _onClose = onClose;

  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();

    // --- Annex State ---
    var annexEntries = UseState<List<RevenueEntry>>([]);
    var annexSelectedId = UseState<int?>(() => null);
    var annexTitle = UseState("");

    // Load Annex Entries
    UseEffect(() => Task.Run(async () =>
    {
      await using var db = factory.CreateDbContext();
      var results = await db.RevenueEntries
                 .Include(e => e.Source)
                 .OrderByDescending(e => e.RevenueDate)
                 .Take(100)
                 .ToListAsync();
      annexEntries.Set(results);
    }), []);

    var tmpl = _file?.MatchedTemplate;

    var options = annexEntries.Value.Select(e =>
       new Option<int?>($"{e.Description ?? "No Name"} - {e.RevenueDate:MM/dd/yyyy} - {e.Source.DescriptionText} - {e.Amount:C}", e.Id)
    ).ToList();

    var contentHeader = Layout.Vertical().Align(Align.Center).Gap(5).Width(Size.Full())
        .Add(Text.H3("Annex Data"))
        .Add(Text.Label("Attach this file to an existing entry.").Muted())
        .Add(new Spacer().Height(10));

    var content = Layout.Vertical().Gap(5).Width(Size.Full()).Add(contentHeader)
         .Add(new Button("Back", () => _onClose()).Variant(ButtonVariant.Primary).Icon(Icons.ArrowLeft))
         .Add(Text.Muted("Attach the current file content to an existing Revenue Entry."))
         .Add(Layout.Vertical().Gap(5)
             .Add(Text.Label("Entry Title"))
             .Add(annexTitle.ToTextInput().Placeholder("e.g. Q1 Royalty Statement")))
         .Add(Layout.Vertical().Gap(5)
             .Add(Text.Label("Template Used"))
             .Add(Text.Muted(tmpl?.Name ?? "Unknown")))
         .Add(Layout.Vertical().Gap(5)
             .Add(Text.Label("Target Entry"))
             .Add(annexSelectedId.ToSelectInput(options).Placeholder("Search entry...")))
         .Add(new Button("Attach Data", async () =>
         {
           if (annexSelectedId.Value == null) { client.Toast("Select an entry", "Warning"); return; }
           if (_file?.Path == null || _file.MatchedTemplate == null) { client.Toast("No valid file/template", "Warning"); return; }

           // Parse Data
           var data = ExcelDataReaderSheet.ParseData(_file.Path, _file.MatchedTemplate);
           if (data.Count == 0) { client.Toast("No data to attach", "Warning"); return; }

           await using var db = factory.CreateDbContext();
           var entry = await db.RevenueEntries.FindAsync(annexSelectedId.Value);
           if (entry != null)
           {
             // Load existing data
             var existingSheets = new List<object>();
             if (!string.IsNullOrEmpty(entry.JsonData))
             {
               try
               {
                 using var doc = JsonDocument.Parse(entry.JsonData);
                 if (doc.RootElement.ValueKind == JsonValueKind.Array)
                 {
                   if (doc.RootElement.GetArrayLength() > 0)
                   {
                     var first = doc.RootElement[0];
                     if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("FileName", out _))
                     {
                       existingSheets = JsonSerializer.Deserialize<List<object>>(entry.JsonData) ?? [];
                     }
                     else
                     {
                       var rows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(entry.JsonData);
                       existingSheets.Add(new { Title = "Legacy Data", FileName = "Legacy", TemplateName = "Unknown", Rows = rows });
                     }
                   }
                 }
                 else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                 {
                   var obj = JsonSerializer.Deserialize<object>(entry.JsonData);
                   if (obj != null) existingSheets.Add(obj);
                 }
               }
               catch { }
             }

             // Append new sheet
             var payload = new
             {
               Title = annexTitle.Value,
               FileName = _file?.Analysis?.FileName ?? "Unknown File",
               TemplateName = tmpl?.Name ?? "Unknown Template",
               Rows = data
             };
             existingSheets.Add(payload);

             entry.JsonData = JsonSerializer.Serialize(existingSheets);

             // --- Asset Extraction Logic ---
             if (tmpl != null)
             {
               try
               {
                 var mappings = tmpl.GetMappings();
                 var assetCol = mappings.FirstOrDefault(x => x.Value == "Asset").Key;
                 var amountCol = mappings.FirstOrDefault(x => x.Value == "Net" || x.Value == "Amount").Key;
                 var collectionCol = mappings.FirstOrDefault(x => x.Value == "Collection").Key;

                 if (!string.IsNullOrEmpty(assetCol))
                 {
                   var batchAssets = new Dictionary<string, decimal>();
                   var assetCollections = new Dictionary<string, string>();

                   foreach (var row in data)
                   {
                     if (row.TryGetValue(assetCol, out var nameObj))
                     {
                       var name = nameObj?.ToString()?.Trim();
                       if (string.IsNullOrEmpty(name)) continue;

                       decimal amount = 0;
                       if (!string.IsNullOrEmpty(amountCol) && row.TryGetValue(amountCol, out var amountObj))
                       {
                         decimal.TryParse(amountObj?.ToString(), out amount);
                       }

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

                   if (batchAssets.Count > 0)
                   {
                     var names = batchAssets.Keys.ToList();
                     var existingAssets = await db.Assets.Where(a => names.Contains(a.Name)).ToListAsync();
                     var existingNames = existingAssets.Select(a => a.Name).ToHashSet();

                     var newAssets = names.Where(n => !existingNames.Contains(n))
                                              .Select(n => new Asset
                                              {
                                                Name = n,
                                                Type = "Unknown",
                                                Category = tmpl.Category,
                                                Collection = assetCollections.GetValueOrDefault(n) ?? "",
                                                AmountGenerated = 0
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
               }
               catch (Exception ex)
               {
                 client.Toast($"Asset extraction failed: {ex.Message}", "Error");
               }
             }

             await db.SaveChangesAsync();
             client.Toast($"Annexed to {entry.Description}", "Success");
             _onClose();
           }
         }).Variant(ButtonVariant.Primary).Disabled(annexSelectedId.Value == null));

    return new Sheet(
        _ => { _onClose(); return ValueTask.CompletedTask; },
        content,
        "",
        ""
    ).Width(Size.Full());
  }
}
