namespace ArtistInsightTool.Apps.Views;

public class ArtistCampaignsBlade(int artistId) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var refreshToken = this.UseRefreshToken();
        var campaigns = this.UseState<Campaign[]?>();
        var (alertView, showAlert) = this.UseAlert();

        this.UseEffect(async () =>
        {
            await using var db = factory.CreateDbContext();
            campaigns.Set(await db.Campaigns.Where(c => c.ArtistId == artistId).ToArrayAsync());
        }, [ EffectTrigger.AfterInit(), refreshToken ]);

        Action OnDelete(int id)
        {
            return () =>
            {
                showAlert("Are you sure you want to delete this campaign?", result =>
                {
                    if (result.IsOk())
                    {
                        Delete(factory, id);
                        refreshToken.Refresh();
                    }
                }, "Delete Campaign", AlertButtonSet.OkCancel);
            };
        }

        if (campaigns.Value == null) return null;

        var table = campaigns.Value.Select(c => new
            {
                Name = c.Name,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                _ = Layout.Horizontal().Gap(2)
                    | Icons.Ellipsis
                        .ToButton()
                        .Ghost()
                        .WithDropDown(MenuItem.Default("Delete").Icon(Icons.Trash).HandleSelect(OnDelete(c.Id)))
                    | Icons.Pencil
                        .ToButton()
                        .Outline()
                        .Tooltip("Edit")
                        .ToTrigger((isOpen) => new ArtistCampaignsEditSheet(isOpen, refreshToken, c.Id))
            })
            .ToTable()
            .RemoveEmptyColumns();

        var addBtn = new Button("Add Campaign").Icon(Icons.Plus).Outline()
            .ToTrigger((isOpen) => new ArtistCampaignsCreateDialog(isOpen, refreshToken, artistId));

        return new Fragment()
               | BladeHelper.WithHeader(addBtn, table)
               | alertView;
    }

    public void Delete(ArtistInsightToolContextFactory factory, int campaignId)
    {
        using var db = factory.CreateDbContext();
        db.Campaigns.Remove(db.Campaigns.Single(c => c.Id == campaignId));
        db.SaveChanges();
    }
}