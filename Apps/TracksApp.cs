using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Music, path: ["Apps"])]
public class TracksApp : ViewBase
{
    public override object? Build()
    {
        return this.UseBlades(() => new TrackListBlade(), "Search");
    }
}
