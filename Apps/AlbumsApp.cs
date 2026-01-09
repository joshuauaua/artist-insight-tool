using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Disc, path: ["Apps"])]
public class AlbumsApp : ViewBase
{
    public override object? Build()
    {
        return this.UseBlades(() => new AlbumListBlade(), "Search");
    }
}
