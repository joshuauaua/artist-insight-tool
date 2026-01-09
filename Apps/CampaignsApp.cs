using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.Flag, path: ["Apps"])]
public class CampaignsApp : ViewBase
{
    public override object? Build()
    {
        return this.UseBlades(() => new CampaignListBlade(), "Search");
    }
}
