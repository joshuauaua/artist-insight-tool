using System.Text.Json;

namespace ArtistInsightTool.Apps.Views;

public class RevenueSourceCreateDialog(IState<bool> isOpen, RefreshToken refreshToken) : ViewBase
{
  private record CreateRequest
  {
    [Required]
    public string Name { get; init; } = "";
  }

  private record ImportRequest
  {
    [Required]
    public string ImportText { get; init; } = "";
  }

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var manualRequest = UseState(() => new CreateRequest());
    var importRequest = UseState(() => new ImportRequest());

    UseEffect(() =>
    {
      using var db = factory.CreateDbContext();
      db.RevenueSources.Add(new RevenueSource { DescriptionText = manualRequest.Value.Name });
      db.SaveChanges();
      refreshToken.Refresh();
    }, [manualRequest]);

    UseEffect(() =>
    {
      ProcessImport(factory, importRequest.Value.ImportText);
      refreshToken.Refresh();
    }, [importRequest]);

    var manualForm = manualRequest
        .ToForm()
        .Builder(r => r.Name, b => b.ToTextInput())
        .ToDialog(isOpen, title: "Create Revenue Source", submitTitle: "Create");

    var importForm = importRequest
        .ToForm()
        .Builder(r => r.ImportText, b => b.ToTextAreaInput().Height(Size.Units(40)))
        .ToDialog(isOpen, title: "Import Revenue Sources", submitTitle: "Import");

    return Layout.Vertical().Gap(20)
        | manualForm
        | importForm;
  }

  private void ProcessImport(ArtistInsightToolContextFactory factory, string text)
  {
    if (string.IsNullOrWhiteSpace(text)) return;

    List<string> names = [];
    text = text.Trim();

    if (text.StartsWith("[") && text.EndsWith("]"))
    {
      try
      {
        names = JsonSerializer.Deserialize<List<string>>(text) ?? [];
      }
      catch
      {
        // Fallback to line splitting if JSON fails
        names = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
      }
    }
    else
    {
      names = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    using var db = factory.CreateDbContext();
    foreach (var name in names)
    {
      if (!string.IsNullOrWhiteSpace(name))
      {
        db.RevenueSources.Add(new RevenueSource { DescriptionText = name });
      }
    }
    db.SaveChanges();
  }
}
