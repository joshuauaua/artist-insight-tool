using ArtistInsightTool.Apps.Views;
using Ivy.Shared; // Ensure ViewBase is available

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Code, path: ["Integrations", "JSON Import"])]
public class JsonImportApp : ViewBase
{
  public override object? Build()
  {
    return new JsonImportView();
  }
}
