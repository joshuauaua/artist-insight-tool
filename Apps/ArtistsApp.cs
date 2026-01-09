using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Music, path: ["Apps"])]
public class ArtistsApp : ViewBase
{
    public override object? Build()
    {
        return this.UseBlades(() => new ArtistListBlade(), "Search");
    }
}
