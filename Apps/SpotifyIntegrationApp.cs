using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Music, title: "Spotify Integration", path: ["Pages"])]
public class SpotifyIntegrationApp : ViewBase
{
  public override object? Build()
  {
    return new SpotifyIntegrationView();
  }
}
