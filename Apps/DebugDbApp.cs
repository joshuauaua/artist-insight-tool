
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Ivy.Shared;

namespace ArtistInsightTool.Apps;

public class DebugDbApp : ViewBase
{
  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var output = UseState("Loading...");

    UseEffect(async () =>
    {
      await using var db = factory.CreateDbContext();
      var templates = await db.ImportTemplates.ToListAsync();
      var entries = await db.RevenueEntries.ToListAsync();

      var report = "TEMPLATES:\n";
      foreach (var t in templates) report += $"- {t.Name} (ID: {t.Id})\n";

      report += "\nENTRIES:\n";
      foreach (var e in entries) report += $"- {e.Description} (ID: {e.Id})\n";

      Console.WriteLine("--- DB REPORT START ---");
      Console.WriteLine(report);
      Console.WriteLine("--- DB REPORT END ---");

      output.Set(report);
    }, []);

    return Text.P(output.Value);
  }
}
