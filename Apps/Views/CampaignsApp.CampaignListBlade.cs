namespace ArtistInsightTool.Apps.Views;

public class CampaignListBlade : ViewBase
{
    private record CampaignListRecord(int Id, string Name, string ArtistName);

    public override object? Build()
    {
        var blades = UseContext<IBladeController>();
        var factory = UseService<ArtistInsightToolContextFactory>();
        var refreshToken = this.UseRefreshToken();

        UseEffect(() =>
        {
            if (refreshToken.ReturnValue is int campaignId)
            {
                blades.Pop(this, true);
                blades.Push(this, new CampaignDetailsBlade(campaignId));
            }
        }, [refreshToken]);

        var onItemClicked = new Action<Event<ListItem>>(e =>
        {
            var campaign = (CampaignListRecord)e.Sender.Tag!;
            blades.Push(this, new CampaignDetailsBlade(campaign.Id), campaign.Name);
        });

        ListItem CreateItem(CampaignListRecord record) =>
            new(title: record.Name, subtitle: record.ArtistName, onClick: onItemClicked, tag: record);

        var createBtn = Icons.Plus.ToButton(_ =>
        {
            blades.Pop(this);
        }).Ghost().Tooltip("Create Campaign").ToTrigger((isOpen) => new CampaignCreateDialog(isOpen, refreshToken));

        return new FilteredListView<CampaignListRecord>(
            fetchRecords: (filter) => FetchCampaigns(factory, filter),
            createItem: CreateItem,
            toolButtons: createBtn,
            onFilterChanged: _ =>
            {
                blades.Pop(this);
            }
        );
    }

    private async Task<CampaignListRecord[]> FetchCampaigns(ArtistInsightToolContextFactory factory, string filter)
    {
        await using var db = factory.CreateDbContext();

        var linq = db.Campaigns
            .Include(c => c.Artist)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filter = filter.Trim();
            linq = linq.Where(c => c.Name.Contains(filter) || c.Artist.Name.Contains(filter));
        }

        return await linq
            .OrderByDescending(c => c.CreatedAt)
            .Take(50)
            .Select(c => new CampaignListRecord(c.Id, c.Name, c.Artist.Name))
            .ToArrayAsync();
    }
}