using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using ArtistInsightTool.Apps.Services;
using ExcelDataReaderExample;

using Ivy.Hooks;

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
    var confirmDeleteId = UseState<int?>(() => null);

    // Load Data with UseQuery
    var templatesQuery = UseQuery("templates_list", async (ct) =>
    {
      var templates = await service.GetTemplatesAsync();
      return templates.Select(t => new TemplateItem(
              t.Id,
              $"T{t.Id:D3}",
              t.Name,
              t.Category ?? "Other",
              t.RevenueEntries?.Count ?? 0,
              t.CreatedAt
          )).OrderBy(t => t.RealId).ToList();
    });

    var items = templatesQuery.Value ?? [];
    var refetch = templatesQuery.Mutator.Revalidate;



    // Search State
    var searchQuery = UseState("");

    // Sheet State
    var editId = UseState<int?>(() => null);
    var showCreate = UseState(false);
    var showImportExcel = UseState(false);

    // Filter Logic
    var filteredItems = items.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(searchQuery.Value))
    {
      var q = searchQuery.Value.ToLowerInvariant();
      filteredItems = filteredItems.Where(t => t.Name.ToLowerInvariant().Contains(q) || t.Category.ToLowerInvariant().Contains(q));
    }

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
                     .HandleSelect(() => showImportExcel.Set(true))
             )
        )
        .Add(Layout.Horizontal().Width(Size.Full()).Height(Size.Fit()).Gap(10)
             .Add(searchQuery.ToTextInput().Placeholder("Search templates...").Width(300))
        );

    // Sheets
    object? sheets = null;
    if (editId.Value != null)
    {
      var editOpen = UseState(true);
      // Sync State: if editOpen becomes false, clear editId
      if (!editOpen.Value) editId.Set((int?)null);
      else sheets = new TemplateEditSheet(editOpen, editId.Value.Value, client);
    }
    else if (showCreate.Value)
    {
      var createOpen = UseState(true);
      if (!createOpen.Value)
      {
        showCreate.Set(false);
        refetch();
      }
      else
      {
        // We can reuse CreateTemplateSheet but it needs to be adapted to be a Sheet or just wrapped?
        // The user asked for "Edit Template should be in a sheet". They didn't explicitly say Create must be.
        // But for consistency let's try to wrap it or just leave it as Diagram for Create?
        // Let's keep Create as Dialog for now to minimize risk, or adapt it.
        // Existing CreateTemplateSheet is a Dialog. I'll leave it.
        sheets = new CreateTemplateSheet(() => { showCreate.Set(false); refetch(); });
      }
    }
    else if (showImportExcel.Value)
    {
      sheets = new ExcelDataReaderSheet(() =>
      {
        showImportExcel.Set(false);
        refetch();
      });
    }

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
                    refetch();
                  }
                  confirmDeleteId.Set((int?)null);
                }).Variant(ButtonVariant.Destructive))
        ) : null,
        sheets
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


public class TemplateEditSheet : ViewBase
{
  private readonly IState<bool> _isOpen;
  private readonly int _templateId;
  private readonly IClientProvider _client;

  public TemplateEditSheet(IState<bool> isOpen, int templateId, IClientProvider client)
  {
    _isOpen = isOpen;
    _templateId = templateId;
    _client = client;
  }

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var template = UseState<ImportTemplate?>(() => null);

    // Mappings State
    var name = UseState("");
    var category = UseState("");
    var headers = UseState<List<string>>([]);

    // Column Mappings
    var assetColumn = UseState<string?>(() => null);
    var amountColumn = UseState<string?>(() => null);
    var sourceColumn = UseState<string?>(() => null); // LabelColumn often maps to Source
    var collectionColumn = UseState<string?>(() => null);
    var currencyColumn = UseState<string?>(() => null);
    // Add other columns as needed

    async Task<IDisposable?> LoadTemplate()
    {
      var templates = await service.GetTemplatesAsync();
      var t = templates.FirstOrDefault(x => x.Id == _templateId);
      if (t != null)
      {
        template.Set(t);
        name.Set(t.Name);
        category.Set(t.Category ?? "Other");
        headers.Set(t.GetHeaders());

        assetColumn.Set(t.AssetColumn);
        amountColumn.Set(t.NetColumn); // Map "Net" to Amount
        sourceColumn.Set(t.LabelColumn);
        collectionColumn.Set(t.CollectionColumn);
        currencyColumn.Set(t.CurrencyColumn);
      }
      return null;
    }

    UseEffect(LoadTemplate, []);

    async Task Save()
    {
      if (template.Value == null) return;
      var t = template.Value;
      t.Name = name.Value;
      t.Category = category.Value;
      t.AssetColumn = assetColumn.Value;
      t.NetColumn = amountColumn.Value;
      t.LabelColumn = sourceColumn.Value;
      t.CollectionColumn = collectionColumn.Value;
      t.CurrencyColumn = currencyColumn.Value;
      t.UpdatedAt = DateTime.UtcNow;

      t.RevenueEntries = []; // Prevent cycle/payload issues
      var success = await service.UpdateTemplateAsync(_templateId, t);
      if (success)
      {
        _client.Toast("Template updated successfully");
        _isOpen.Set(false);
      }
      else
      {
        _client.Toast("Failed to update template", "Error");
      }
    }

    void ClearColumn(string header)
    {
      if (assetColumn.Value == header) assetColumn.Set((string?)null);
      if (amountColumn.Value == header) amountColumn.Set((string?)null);
      if (sourceColumn.Value == header) sourceColumn.Set((string?)null);
      if (collectionColumn.Value == header) collectionColumn.Set((string?)null);
      if (currencyColumn.Value == header) currencyColumn.Set((string?)null);
    }

    var mappingSection = Layout.Vertical().Gap(12).Align(Align.Stretch);
    if (headers.Value.Count > 0)
    {
      // mappingSection.Add(Text.H5("Column Mappings")); // Removed title from here

      foreach (var header in headers.Value)
      {
        var currentRole = "Ignore";
        if (assetColumn.Value == header) currentRole = "Asset";
        else if (amountColumn.Value == header) currentRole = "Net (Amount)";
        else if (sourceColumn.Value == header) currentRole = "Source/Label";
        else if (collectionColumn.Value == header) currentRole = "Collection";
        else if (currencyColumn.Value == header) currentRole = "Currency";

        mappingSection.Add(Layout.Horizontal().Gap(10).Align(Align.Center)
            .Add(Layout.Vertical().Width(Size.Fraction(1)).Align(Align.Left)
                .Add(Text.Label(header)))
            .Add(Layout.Vertical().Width(150)
                .Add(new DropDownMenu(
                    DropDownMenu.DefaultSelectHandler(),
                    new Button(currentRole).Variant(ButtonVariant.Outline).Icon(Icons.ChevronDown).Width(Size.Full())
                )
                | MenuItem.Default("Ignore").HandleSelect(() => ClearColumn(header))
                | MenuItem.Default("Asset").HandleSelect(() => { ClearColumn(header); assetColumn.Set(header); })
                | MenuItem.Default("Net (Amount)").HandleSelect(() => { ClearColumn(header); amountColumn.Set(header); })
                | MenuItem.Default("Source/Label").HandleSelect(() => { ClearColumn(header); sourceColumn.Set(header); })
                | MenuItem.Default("Collection").HandleSelect(() => { ClearColumn(header); collectionColumn.Set(header); })
                | MenuItem.Default("Currency").HandleSelect(() => { ClearColumn(header); currencyColumn.Set(header); })
                )
            )
        );
      }
    }
    else
    {
      mappingSection.Add(Text.Muted("No headers found in this template."));
    }

    var content = Layout.Vertical().Gap(10).Padding(40, 10).Align(Align.Stretch)
        .Add(new Card(
            Layout.Vertical().Gap(15)
                .Add(Layout.Vertical().Gap(5)
                     .Add(Text.Label("Template Name"))
                     .Add(name.ToTextInput()))
                .Add(Layout.Vertical().Gap(5)
                     .Add(Text.Label("Category"))
                     .Add(category.ToSelectInput(new[] { "Merchandise", "Royalties", "Concerts", "Other" }.Select(c => new Option<string>(c, c)))))
            ).Title("Template Details")
        )
        .Add(new Card(
            mappingSection
            ).Title("Column Mappings")
        );

    var footer = Layout.Horizontal().Gap(10).Align(Align.Right).Padding(6)
        .Add(new Button("Cancel", () => _isOpen.Set(false)).Variant(ButtonVariant.Ghost))
        .Add(new Button("Save Changes", async () => await Save()).Variant(ButtonVariant.Primary));


    return new Sheet(
        _ => _isOpen.Set(false),
        new FooterLayout(footer, content),
        "Edit Template",
        "Modify template details and column mappings."
    ).Width(Size.Full());
  }
}
