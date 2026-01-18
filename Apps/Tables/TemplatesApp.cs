using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;

namespace ArtistInsightTool.Apps.Tables;

[App(icon: Icons.Sheet, title: "Templates Table", path: ["Tables"])]
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

    // Search State
    var searchQuery = UseState("");

    // Filter Logic
    var filteredItems = items.Value.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(searchQuery.Value))
    {
      var q = searchQuery.Value.ToLowerInvariant();
      filteredItems = filteredItems.Where(t => t.Name.ToLowerInvariant().Contains(q) || t.Category.ToLowerInvariant().Contains(q));
    }

    // Table Data
    var tableData = filteredItems.Select(t => new
    {
      t.RealId,
      t.Id,
      t.Name,
      t.Category,
      t.Files,
      Actions = new Button("", async () => await DeleteTemplate(t.RealId)).Icon(Icons.Trash).Variant(ButtonVariant.Destructive)
    }).AsQueryable();

    var headerContent = Layout.Vertical()
        .Width(Size.Full())
        .Height(Size.Fit())
        .Gap(10)
        .Padding(20, 20, 20, 5)
        .Add(Layout.Horizontal().Width(Size.Full()).Height(Size.Fit()).Align(Align.Center)
             .Add("Templates Table")
             .Add(new Spacer().Width(Size.Fraction(1)))
             .Add(new Button("Refresh", () => refresh.Set(refresh.Value + 1)).Variant(ButtonVariant.Outline))
        )
        .Add(Layout.Horizontal().Width(Size.Full()).Height(Size.Fit()).Gap(10)
             .Add(searchQuery.ToTextInput().Placeholder("Search templates...").Width(300))
        );

    return new Fragment(
        Layout.Vertical().Height(Size.Full()).Gap(0)
            .Add(headerContent)
            .Add(Layout.Vertical().Height(Size.Fraction(1)).Padding(20, 0, 20, 50)
                .Add(
                     tableData.ToTable()
                     .Width(Size.Full())
                     .Header(x => x.Id, "ID")
                     .Header(x => x.Name, "Name")
                     .Header(x => x.Category, "Category")
                     .Header(x => x.Files, "Linked Files")
                     .Header(x => x.Actions, "")
                )
            )
    );
  }
}
