using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using ArtistInsightTool.Apps.Services;
using ExcelDataReaderExample;
using System.Text.Json;

using Ivy.Hooks;

namespace ArtistInsightTool.Apps.Tables;

// [App(icon: Icons.Sheet, title: "Templates Table", path: ["Tables"])]
public class TemplatesApp : ViewBase
{
  // Define Table Item
  public record TemplateItem(int RealId, string Id, string Name, string Source, string Category, int Files, DateTime CreatedAt);

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var client = UseService<IClientProvider>();
    var confirmDeleteId = UseState<int?>(() => null);

    // Load Data with UseQuery
    var templatesQuery = UseQuery("templates_list", async (ct) => await service.GetTemplatesAsync());

    var items = templatesQuery.Value?.Select(t =>
    {
      int fileCount = 0;
      if (t.RevenueEntries != null)
      {
        foreach (var entry in t.RevenueEntries)
        {
          if (!string.IsNullOrEmpty(entry.JsonData))
          {
            try
            {
              var data = JsonSerializer.Deserialize<List<object>>(entry.JsonData);
              fileCount += data?.Count ?? 0;
            }
            catch { fileCount++; } // Fallback to 1 if not JSON
          }
          else if (!string.IsNullOrEmpty(entry.FileName))
          {
            fileCount++;
          }
        }
      }

      return new TemplateItem(
          t.Id,
          $"T{t.Id:D3}",
          t.Name,
          t.SourceName ?? "-",
          t.Category ?? "Other",
          fileCount,
          t.CreatedAt
      );
    }).OrderBy(t => t.RealId).ToList() ?? [];

    if (templatesQuery.Loading && items.Count == 0) return Layout.Center().Add(Text.Label("Loading templates...").Muted());
    var refetch = templatesQuery.Mutator.Revalidate;



    // Search State
    var searchQuery = UseState("");

    // Sheet State
    var editId = UseState<int?>(() => null);
    var viewTemplateId = UseState<int?>(() => null);
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
      IdButton = new Button(t.Id, () => viewTemplateId.Set(t.RealId)).Variant(ButtonVariant.Ghost),
      t.Name,
      t.Source,
      t.Category,
      t.Files,
      Actions = Layout.Horizontal().Gap(5)
              .Add(new Button("", () => editId.Set(t.RealId)).Icon(Icons.Pencil).Variant(ButtonVariant.Ghost))
              .Add(new Button("", () => confirmDeleteId.Set(t.RealId)).Icon(Icons.Trash).Variant(ButtonVariant.Ghost))
    }).AsQueryable();

    var headerCard = new Card(
        Layout.Vertical().Gap(10)
            .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                 .Add(Text.H1("Templates Table"))
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
            .Add(Layout.Horizontal().Width(Size.Full()).Gap(10)
                 .Add(searchQuery.ToTextInput().Placeholder("Search templates...").Width(300))
            )
    );

    // Sheets (Logic remains same)
    object? sheets = null;
    if (editId.Value != null)
    {
      sheets = new TemplateEditSheet(() => editId.Set((int?)null), editId.Value.Value, client);
    }
    else if (viewTemplateId.Value != null)
    {
      sheets = new TemplateDataViewSheet(viewTemplateId.Value.Value, () => viewTemplateId.Set((int?)null));
    }
    else if (showCreate.Value)
    {
      sheets = new CreateTemplateSheet(() => { showCreate.Set(false); refetch(); });
    }
    else if (showImportExcel.Value)
    {
      sheets = new ExcelDataReaderSheet(() =>
      {
        showImportExcel.Set(false);
        refetch();
      });
    }

    var content = Layout.Vertical().Height(Size.Full()).Padding(20, 0, 20, 50)
                .Add(filteredItems.Any()
                     ? tableData.ToTable()
                     .Width(Size.Full())
                     .Add(x => x.IdButton)
                     .Header(x => x.IdButton, "ID")
                     .Header(x => x.Name, "Name")
                     .Header(x => x.Source, "Source")
                     .Header(x => x.Category, "Category")
                     .Header(x => x.Files, "Linked Files")
                     .Header(x => x.Actions, "")
                     : Layout.Center().Add(Text.Label("There is no information to display"))
                );

    return new Fragment(
        new HeaderLayout(headerCard, content),
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
    var sourceName = UseState("");
    var category = UseState("Other");

    return new Dialog(
        _ => onClose(),
        new DialogHeader(""),
        new DialogBody(
            Layout.Vertical().Gap(10)
            .Add(Layout.Vertical().Align(Align.Center).Gap(5).Width(Size.Full())
                .Add(Text.H3("Create Template"))
                .Add(Text.Label("Define a new import template manually.").Muted())
                .Add(new Spacer().Height(10))
            )
            .Add(Text.Label("Template Name"))
            .Add(name.ToTextInput().Placeholder("e.g. Distrokid CSV"))
            .Add(Text.Label("Source Name"))
            .Add(sourceName.ToTextInput().Placeholder("e.g. Distrokid"))
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
                    SourceName = sourceName.Value,
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
  private readonly Action _onClose;
  private readonly int _templateId;
  private readonly IClientProvider _client;

  public TemplateEditSheet(Action onClose, int templateId, IClientProvider client)
  {
    _onClose = onClose;
    _templateId = templateId;
    _client = client;
  }

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var template = UseState<ImportTemplate?>(() => null);

    // Mappings State
    var name = UseState("");
    var sourceName = UseState("");
    var category = UseState("");
    var headers = UseState<List<string>>([]);
    var mappings = UseState<Dictionary<string, string>>(() => new());

    async Task<IDisposable?> LoadTemplate()
    {
      var templates = await service.GetTemplatesAsync();
      var t = templates.FirstOrDefault(x => x.Id == _templateId);
      if (t != null)
      {
        template.Set(t);
        name.Set(t.Name);
        sourceName.Set(t.SourceName ?? "");
        category.Set(t.Category ?? "Other");
        headers.Set(t.GetHeaders());
        mappings.Set(t.GetMappings());
      }
      return null;
    }

    UseEffect(LoadTemplate, []);

    async Task Save()
    {
      if (template.Value == null) return;
      var t = template.Value;
      t.Name = name.Value;
      t.SourceName = sourceName.Value;
      t.Category = category.Value;
      t.MappingsJson = JsonSerializer.Serialize(mappings.Value);
      t.UpdatedAt = DateTime.UtcNow;

      t.RevenueEntries = []; // Prevent cycle/payload issues
      var success = await service.UpdateTemplateAsync(_templateId, t);
      if (success)
      {
        _client.Toast("Template updated successfully");
        _onClose();
      }
      else
      {
        _client.Toast("Failed to update template", "Error");
      }
    }

    void SetHeaderMapping(string header, string systemField)
    {
      var newMap = mappings.Value.ToDictionary(x => x.Key, x => x.Value);
      if (systemField == "Ignore")
      {
        newMap.Remove(header);
      }
      else
      {
        // Remove existing mapping for this field if it's a unique field (one header per field)
        // For now, let's just allow it or keep it simple
        newMap[header] = systemField;
      }
      mappings.Set(newMap);
    }

    var fieldOptions = new[]
    {
        "Artist", "AssetTitle", "AssetType", "Currency", "Date", "DSP", "Fees", "Gross", "Id", "Ignore", "Net", "ProductArtist", "ProductName", "Store", "Territory"
    };

    var mappingSection = Layout.Vertical().Gap(12).Align(Align.Stretch);
    if (headers.Value.Count > 0)
    {
      foreach (var header in headers.Value)
      {
        var currentRole = mappings.Value.TryGetValue(header, out var role) ? role : "Ignore";

        var menu = new DropDownMenu(
            DropDownMenu.DefaultSelectHandler(),
            new Button(currentRole).Variant(ButtonVariant.Outline).Icon(Icons.ChevronDown).Width(Size.Full())
        );
        foreach (var opt in fieldOptions)
        {
          menu = menu | MenuItem.Default(opt).HandleSelect(() => SetHeaderMapping(header, opt));
        }

        mappingSection.Add(Layout.Horizontal().Gap(10).Align(Align.Center)
            .Add(Layout.Vertical().Width(Size.Fraction(1)).Align(Align.Left)
                .Add(Text.Label(header)))
            .Add(Layout.Vertical().Width(150)
                .Add(menu)
            )
        );
      }
    }
    else
    {
      mappingSection.Add(Text.Muted("No headers found in this template."));
    }

    var contentHeader = Layout.Vertical().Align(Align.Center).Gap(5).Width(Size.Full())
        .Add(Text.H3("Edit Template"))
        .Add(Text.Label("Modify template details and column mappings.").Muted())
        .Add(new Spacer().Height(10));

    var content = Layout.Vertical().Gap(10).Padding(40, 10).Align(Align.Stretch).Add(contentHeader)
        .Add(new Card(
            Layout.Vertical().Gap(15)
                .Add(Layout.Vertical().Gap(5)
                     .Add(Text.Label("Template Name"))
                     .Add(name.ToTextInput()))
                .Add(Layout.Vertical().Gap(5)
                     .Add(Text.Label("Source Name"))
                     .Add(sourceName.ToTextInput()))
                .Add(Layout.Vertical().Gap(5)
                     .Add(Text.Label("Category"))
                     .Add(category.ToSelectInput(new[] { "Merchandise", "Royalties", "Concerts", "Other" }.Select(c => new Option<string>(c, c)))))
            ).Title("Template Details")
        )
        .Add(new Card(
            mappingSection
            ).Title("Column Mappings")
        );

    var footer = Layout.Horizontal().Gap(10).Align(Align.Center).Padding(6)
        .Add(new Button("Save Changes", async () => await Save()).Variant(ButtonVariant.Primary));


    return new Sheet(
        _ => _onClose(),
        new FooterLayout(footer, content),
        "",
        ""
    ).Width(Size.Full());
  }
}
