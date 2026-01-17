using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Ivy.Shared; // Check if needed, probably strictly accessing Context

// Standalone script context if needed, or just use the app's context
// Assuming running via 'dotnet run' in a way that executes this, but usually I need to piggyback on the app or use a separate console app.
// Since I can't easily run a separate console app without csproj mods, I will inject this into a temporary view or just modify Program.cs temporarily? 
// Better: Create a temporary generic App that runs on startup or just a simple view I can look at.
// Actually, I can just write a small "DebugApp.cs" that lists the raw data.

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Database, title: "DB Inspector", path: ["System"])]
public class DbInspectorApp : ViewBase
{
  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var output = UseState("Loading...");

    UseEffect(async () =>
    {
      try
      {
        await using var db = factory.CreateDbContext();
        var entries = await db.RevenueEntries.Include(e => e.Source).ToListAsync();
        var sources = await db.RevenueSources.ToListAsync();
        var assets = await db.Assets.Take(10).ToListAsync();

        var report = $"Entries Count: {entries.Count}\n";
        foreach (var e in entries)
        {
          report += $" - ID: {e.Id}, Desc: {e.Description}, DataLen: {e.JsonData?.Length ?? 0}, Source: {e.Source?.DescriptionText ?? "NULL"}, Created: {e.CreatedAt}\n";
        }

        report += $"\nSources Count: {sources.Count}\n";
        foreach (var s in sources)
        {
          report += $" - ID: {s.Id}, Desc: {s.DescriptionText}\n";
        }


        report += $"\nTotal Assets: {await db.Assets.CountAsync()}\n";
        foreach (var a in assets)
        {
          report += $" - [{a.Id:D3}] {a.Name} ({a.Type})\n";
        }
        if (assets.Count < await db.Assets.CountAsync()) report += " ... (more)\n";

        report += "\n--- Data Tables Analysis ---\n";
        foreach (var e in entries.Where(x => !string.IsNullOrWhiteSpace(x.JsonData)))
        {
          try
          {
            using var doc = JsonDocument.Parse(e.JsonData);
            var root = doc.RootElement;
            int tableCount = 0;
            string info = "";

            if (root.ValueKind == JsonValueKind.Array)
            {
              if (root.GetArrayLength() > 0)
              {
                var first = root[0];
                bool isMulti = first.ValueKind == JsonValueKind.Object && (first.TryGetProperty("FileName", out _) || first.TryGetProperty("fileName", out _));

                if (isMulti)
                {
                  tableCount = root.GetArrayLength();
                  info = $"Multi-sheet ({tableCount} tables)";
                }
                else
                {
                  tableCount = 1;
                  info = "Legacy (1 table)";
                }
              }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
              tableCount = 1;
              info = "Single Sheet (1 table)";
            }

            report += $" - Entry {e.Id}: {info}\n";
          }
          catch (Exception ex)
          {
            report += $" - Entry {e.Id}: Error parsing JSON - {ex.Message}\n";
          }
        }

        try
        {
          File.WriteAllText("/Users/joshuang/Desktop/Programming/Ivy/artist-insight-tool/db_dump.txt", report);
        }
        catch { }

        output.Set(report);
      }
      catch (Exception ex)
      {
        output.Set($"Error: {ex.Message}\n{ex.StackTrace}");
      }
    }, []);

    return Layout.Vertical().Padding(20).Gap(10)
        .Add(Text.H1("DB Inspector"))
        .Add(new Markdown($"```text\n{output.Value}\n```"));
  }
}
