using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using ArtistInsightTool.Apps.Services;

namespace ArtistInsightTool.Apps.Tables;

[App(icon: Icons.Sheet, title: "Templates Table", path: ["Tables"])]
public class TemplatesApp : ViewBase
{
  // Define Table Item
  public record TemplateItem(int RealId, string Id, string Name, string Category, int Files, DateTime CreatedAt);

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var client = UseService<IClientProvider>();
    var items = UseState<List<TemplateItem>>([]);
    var refresh = UseState(0);
    var confirmDeleteId = UseState<int?>(() => null);

    // Load Data
    UseEffect(async () =>
    {
      try
      {
        await Task.Delay(10);
        // Load Data from API
        var templates = await service.GetTemplatesAsync();

        var mapped = templates.Select(t => new TemplateItem(
                t.Id,
                $"T{t.Id:D3}",
                t.Name,
                t.Category ?? "Other",
                t.RevenueEntries?.Count ?? 0,
                t.CreatedAt
            )).OrderBy(t => t.RealId).ToList();

        items.Set(mapped);
      }
      catch
      {

      }
    }, [EffectTrigger.AfterInit(), refresh]);



    // Search State
    var searchQuery = UseState("");

    // Filter Logic
    var filteredItems = items.Value.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(searchQuery.Value))
    {
      var q = searchQuery.Value.ToLowerInvariant();
      filteredItems = filteredItems.Where(t => t.Name.ToLowerInvariant().Contains(q) || t.Category.ToLowerInvariant().Contains(q));
    }

    var editId = UseState<int?>(() => null);

    // Table Data
    var tableData = filteredItems.Select(t => new
    {
      IdButton = new Button(t.Id, () => { }).Variant(ButtonVariant.Ghost),
      t.Name,
      t.Category,
      t.Files,
      Actions = Layout.Horizontal().Gap(5)
              .Add(new Button("", () => editId.Set(t.RealId)).Icon(Icons.Pencil).Variant(ButtonVariant.Ghost))
              .Add(new Button("", () => confirmDeleteId.Set(t.RealId)).Icon(Icons.Trash).Variant(ButtonVariant.Ghost))
    }).AsQueryable();

    var showCreate = UseState(false);

    if (editId.Value != null)
    {
      return new EditTemplateSheet(editId.Value.Value, () =>
      {
        editId.Set((int?)null);
        refresh.Set(refresh.Value + 1);
      });
    }

    if (showCreate.Value)
    {
      return new CreateTemplateSheet(() =>
      {
        showCreate.Set(false);
        refresh.Set(refresh.Value + 1);
      });
    }

    var headerContent = Layout.Vertical()
        .Width(Size.Full())
        .Height(Size.Fit())
        .Gap(10)
        .Padding(20, 20, 20, 5)
        .Add(Layout.Horizontal().Width(Size.Full()).Height(Size.Fit()).Align(Align.Center)
             .Add("Templates Table")
             .Add(new Spacer().Width(Size.Fraction(1)))
             .Add(new DropDownMenu(
                     DropDownMenu.DefaultSelectHandler(),
                     new Button("Create Template").Icon(Icons.Plus).Variant(ButtonVariant.Primary)
                 )
                 | MenuItem.Default("Manual Entry").Icon(Icons.Plus)
                     .HandleSelect(() => showCreate.Set(true))
                 | MenuItem.Default("From Excel File").Icon(Icons.FileSpreadsheet)
                     .HandleSelect(() => client.Toast("Please use the 'Excel Data Reader' application to create templates from files.", "Info"))
             )
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
                     .Add(x => x.IdButton)
                     .Header(x => x.IdButton, "ID")
                     .Header(x => x.Name, "Name")
                     .Header(x => x.Category, "Category")
                     .Header(x => x.Files, "Linked Files")
                     .Header(x => x.Actions, "")
                )
            ),
        confirmDeleteId.Value != null ? new Dialog(
            _ => confirmDeleteId.Set((int?)null),
            new DialogHeader("Confirm Deletion"),
            new DialogBody(Text.Label("Are you sure you want to delete this template? This action cannot be undone.")),
            new DialogFooter(
                new Button("Cancel", () => { confirmDeleteId.Set((int?)null); }),
                new Button("Delete", async () =>
                {
                  if (confirmDeleteId.Value == null) return;
                  var success = await service.DeleteTemplateAsync(confirmDeleteId.Value.Value);
                  if (success)
                  {
                    refresh.Set(refresh.Value + 1);
                  }
                  confirmDeleteId.Set((int?)null);
                }).Variant(ButtonVariant.Destructive))
        ) : null
    );
  }
}

public class CreateTemplateSheet(Action onClose) : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var name = UseState("");
    var category = UseState("Other");

    return new Dialog(
        _ => onClose(),
        new DialogHeader("Create Template"),
        new DialogBody(
            Layout.Vertical().Gap(10)
            .Add(Text.Label("Template Name"))
            .Add(name.ToTextInput().Placeholder("e.g. Distrokid CSV"))
            .Add(Text.Label("Category"))
            .Add(category.ToSelectInput(new[] { "Merchandise", "Royalties", "Concerts", "Other" }.Select(c => new Option<string>(c, c))))
            .Add(Layout.Horizontal().Align(Align.Right).Gap(10).Padding(10, 0, 0, 0)
                .Add(new Button("Cancel", onClose))
                .Add(new Button("Create", async () =>
                {
                  if (string.IsNullOrWhiteSpace(name.Value)) return;

                  await service.CreateTemplateAsync(new ImportTemplate
                  {
                    Name = name.Value,
                    Category = category.Value,
                    HeadersJson = "[]",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                  });
                  onClose();
                }).Variant(ButtonVariant.Primary))
            )
        ),
        new DialogFooter()
    );
  }
}

public class EditTemplateSheet(int id, Action onClose) : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var name = UseState("");
    var category = UseState("");
    var originalTemplate = UseState<ImportTemplate?>(() => null);

    UseEffect(async () =>
    {
      var templates = await service.GetTemplatesAsync();
      var t = templates.FirstOrDefault(x => x.Id == id);
      if (t != null)
      {
        name.Set(t.Name);
        category.Set(t.Category ?? "Other");
        originalTemplate.Set(t);
      }
    }, []);

    return new Dialog(
        _ => onClose(),
        new DialogHeader("Edit Template"),
        new DialogBody(
            Layout.Vertical().Gap(10)
            .Add(Text.Label("Template Name"))
            .Add(name.ToTextInput().Placeholder("e.g. Distrokid CSV"))
            .Add(Text.Label("Category"))
            .Add(category.ToSelectInput(new[] { "Merchandise", "Royalties", "Concerts", "Other" }.Select(c => new Option<string>(c, c))))
            .Add(Layout.Horizontal().Align(Align.Right).Gap(10).Padding(10, 0, 0, 0)
                .Add(new Button("Cancel", onClose))
                .Add(new Button("Save", async () =>
                {
                  if (string.IsNullOrWhiteSpace(name.Value) || originalTemplate.Value == null) return;

                  var t = originalTemplate.Value;
                  t.Name = name.Value;
                  t.Category = category.Value;
                  t.UpdatedAt = DateTime.UtcNow;

                  await service.UpdateTemplateAsync(id, t);
                  onClose();
                }).Variant(ButtonVariant.Primary))
            )
        ),
        new DialogFooter()
    );
  }
}
