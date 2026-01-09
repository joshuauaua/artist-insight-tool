namespace ArtistInsightTool.Apps.Views;

public class CampaignDetailsBlade(int campaignId) : ViewBase
{
    public override object? Build()
    {
        var factory = this.UseService<ArtistInsightToolContextFactory>();
        var blades = this.UseContext<IBladeController>();
        var refreshToken = this.UseRefreshToken();
        var campaign = this.UseState<Campaign?>();
        var revenueEntryCount = this.UseState<int>();
        var (alertView, showAlert) = this.UseAlert();

        this.UseEffect(async () =>
        {
            var db = factory.CreateDbContext();
            campaign.Set(await db.Campaigns
                .Include(e => e.Artist)
                .SingleOrDefaultAsync(e => e.Id == campaignId));
            revenueEntryCount.Set(await db.RevenueEntries.CountAsync(e => e.CampaignId == campaignId));
        }, [EffectTrigger.AfterInit(), refreshToken]);

        if (campaign.Value == null) return null;

        var campaignValue = campaign.Value;

        void OnDelete()
        {
            showAlert("Are you sure you want to delete this campaign?", result =>
            {
                if (result.IsOk())
                {
                    Delete(factory);
                    blades.Pop(refresh: true);
                }
            }, "Delete Campaign", AlertButtonSet.OkCancel);
        };

        var dropDown = Icons.Ellipsis
            .ToButton()
            .Ghost()
            .WithDropDown(
                MenuItem.Default("Delete").Icon(Icons.Trash).HandleSelect(OnDelete)
            );

        var editBtn = new Button("Edit")
            .Outline()
            .Icon(Icons.Pencil)
            .ToTrigger((isOpen) => new CampaignEditSheet(isOpen, refreshToken, campaignId));

        var detailsCard = new Card(
            content: new
                {
                    campaignValue.Id,
                    campaignValue.Name,
                    ArtistName = campaignValue.Artist.Name,
                    StartDate = campaignValue.StartDate?.ToString("yyyy-MM-dd") ?? "N/A",
                    EndDate = campaignValue.EndDate?.ToString("yyyy-MM-dd") ?? "N/A"
                }.ToDetails()
                .RemoveEmpty()
                .Builder(e => e.Id, e => e.CopyToClipboard()),
            footer: Layout.Horizontal().Gap(2).Align(Align.Right)
                    | dropDown
                    | editBtn
        ).Title("Campaign Details").Width(Size.Units(100));

        var relatedCard = new Card(
            new List(
                new ListItem("Revenue Entries", onClick: _ =>
                {
                    blades.Push(this, new CampaignRevenueEntriesBlade(campaignId), "Revenue Entries");
                }, badge: revenueEntryCount.Value.ToString("N0"))
            ));

        return new Fragment()
               | (Layout.Vertical() | detailsCard | relatedCard)
               | alertView;
    }

    private void Delete(ArtistInsightToolContextFactory dbFactory)
    {
        using var db = dbFactory.CreateDbContext();
        var campaign = db.Campaigns.FirstOrDefault(e => e.Id == campaignId)!;
        db.Campaigns.Remove(campaign);
        db.SaveChanges();
    }
}