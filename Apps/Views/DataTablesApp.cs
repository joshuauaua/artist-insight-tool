using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ArtistInsightTool.Apps.Views;

[App(icon: Icons.Database, title: "Data Tables", path: ["Pages"])]
public class DataTablesApp : ViewBase
{
  record TableItem(string FileName, string SheetTitle, string TemplateName, string EntryDescription, int EntryId, string Date);

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var refresh = UseState(0);
    var tables = UseState<List<TableItem>>([]);

    UseEffect(async () =>
    {
      await using var db = factory.CreateDbContext();
      var entries = await db.RevenueEntries
          .Where(e => e.JsonData != null && e.JsonData != "")
          .OrderByDescending(e => e.CreatedAt)
          .ToListAsync();

      var items = new List<TableItem>();

      foreach (var entry in entries)
      {
        try
        {
          if (string.IsNullOrWhiteSpace(entry.JsonData)) continue;

          using var doc = JsonDocument.Parse(entry.JsonData);
          if (doc.RootElement.ValueKind == JsonValueKind.Array)
          {
            if (doc.RootElement.GetArrayLength() > 0)
            {
              var first = doc.RootElement[0];
              // New Format: List of Objects with 'FileName' property
              if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("FileName", out _))
              {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                  var fileName = element.GetProperty("FileName").GetString() ?? "Unknown";
                  var sheetTitle = element.TryGetProperty("Title", out var t) ? t.GetString() : "";
                  var templateName = element.TryGetProperty("TemplateName", out var tn) ? tn.GetString() : "Unknown";

                  items.Add(new TableItem(
                      fileName,
                      sheetTitle ?? "",
                      templateName,
                      entry.Description ?? "No Description",
                      entry.Id,
                      entry.UpdatedAt.ToShortDateString()
                  ));
                }
              }
              else
              {
                // Legacy Format: List of Rows
                items.Add(new TableItem(
                    "Legacy Import",
                    "Main Data",
                    "Unknown",
                    entry.Description ?? "No Description",
                    entry.Id,
                    entry.UpdatedAt.ToShortDateString()
                ));
              }
            }
          }
          else if (doc.RootElement.ValueKind == JsonValueKind.Object)
          {
            items.Add(new TableItem(
                "Single Sheet",
                "Data",
                "Unknown",
                entry.Description ?? "No Description",
                entry.Id,
                entry.UpdatedAt.ToShortDateString()
            ));
          }
        }
        catch { }
      }

      tables.Set(items);
      return null;
    }, [refresh]);

    return Layout.Vertical()
        .Padding(20)
        .Gap(20)
        .Add(Text.H3("Data Tables"))
        .Add(tables.Value.Select(t => new
        {
          File = t.FileName,
          Sheet = t.SheetTitle,
          Template = t.TemplateName,
          Entry = t.EntryDescription,
          Date = t.Date
        }).ToArray().ToTable()
            .Header(x => x.File, "File Name")
            .Header(x => x.Sheet, "Sheet Title")
            .Header(x => x.Template, "Template")
            .Header(x => x.Entry, "Revenue Entry")
            .Header(x => x.Date, "Uploaded")
        );
  }
}
