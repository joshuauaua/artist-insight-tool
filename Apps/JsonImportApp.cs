using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Code, path: ["Integrations", "JSON Import"])]
public class JsonImportApp : ViewBase
{
  public override object? Build()
  {
    return new JsonImportView();
  }
}
