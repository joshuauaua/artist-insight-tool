using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;

namespace ArtistInsightTool.Apps.Views;

[App(icon: Icons.Sheet, title: "Templates", path: ["Pages"])]
public class TemplatesApp : ViewBase
{
  // Define Table Item
  public record TemplateItem(int RealId, string Id, string Name, string Category, int Files, DateTime CreatedAt);

  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var items = UseState<List<TemplateItem>>([]);
    var refresh = UseState(0);

    // Load Data
    UseEffect(async () =>
    {
      try
      {
        await Task.Delay(10);
        await using var db = factory.CreateDbContext();

        Console.WriteLine("Templates: Loading...");

        // Fetch templates with usage count
        // We left join RevenueEntries on ImportTemplateId to count usage
        var templates = await db.ImportTemplates
                .GroupJoin(
                    db.RevenueEntries,
                    t => t.Id,
                    e => e.ImportTemplateId,
                    (t, entries) => new
                    {
                      t.Id,
                      t.Name,
                      t.Category,
                      t.CreatedAt,
                      Files = entries.Count()
                    }
                )
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

        var mapped = templates.Select(t => new TemplateItem(
                t.Id,
                $"T{t.Id:D3}",
                t.Name,
                t.Category ?? "Other",
                t.Files,
                t.CreatedAt
            )).ToList();

        items.Set(mapped);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Templates Load Error: {ex.Message}");
      }
    }, [EffectTrigger.AfterInit(), refresh]);

    // Action: Delete Template
    async Task DeleteTemplate(int id)
    {
      await using var db = factory.CreateDbContext();
      var t = await db.ImportTemplates.FindAsync(id);
      if (t != null)
      {
        db.ImportTemplates.Remove(t);
        await db.SaveChangesAsync();
        refresh.Set(refresh.Value + 1);
      }
    }

    // Table Data
    var tableData = items.Value.Select(t => new
    {
      t.RealId,
      t.Id,
      t.Name,
      t.Category,
      t.Files,
      Actions = new Button("", async () => await DeleteTemplate(t.RealId)).Icon(Icons.Trash).Variant(ButtonVariant.Destructive)
    }).AsQueryable();

    return Layout.Vertical().Height(Size.Full()).Padding(20).Gap(20)
        .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
             .Add(Text.H4("Import Templates"))
             .Add(new Spacer())
             .Add(new Button("Refresh", () => refresh.Set(refresh.Value + 1)).Variant(ButtonVariant.Outline))
        )
        .Add(
             tableData.ToTable()
             .Width(Size.Full())
             .Header(x => x.Id, "ID")
             .Header(x => x.Name, "Name")
             .Header(x => x.Category, "Category")
             .Header(x => x.Files, "Linked Files")
             .Header(x => x.Actions, "")
        );
  }
}
