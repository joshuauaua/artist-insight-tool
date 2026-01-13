using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Upload, path: ["Integrations", "CSV Import"])]
public class CsvImportApp : ViewBase
{
  public override object? Build()
  {
    return new CsvImportView();
  }
}
