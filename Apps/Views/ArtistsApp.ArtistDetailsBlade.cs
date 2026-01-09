namespace ArtistInsightTool.Apps.Views;

public class ArtistDetailsBlade(int artistId) : ViewBase
{
    public override object? Build()
    {
        var factory = this.UseService<ArtistInsightToolContextFactory>();
        var blades = this.UseContext<IBladeController>();
        var refreshToken = this.UseRefreshToken();
        var artist = this.UseState<Artist?>();
        var trackCount = this.UseState<int>();
        var albumCount = this.UseState<int>();
        var campaignCount = this.UseState<int>();
        var revenueEntryCount = this.UseState<int>();
        var (alertView, showAlert) = this.UseAlert();

        this.UseEffect(async () =>
        {
            var db = factory.CreateDbContext();
            artist.Set(await db.Artists
                .Include(e => e.Tracks)
                .Include(e => e.Albums)
                .Include(e => e.Campaigns)
                .Include(e => e.RevenueEntries)
                .SingleOrDefaultAsync(e => e.Id == artistId));
            trackCount.Set(await db.Tracks.CountAsync(e => e.ArtistId == artistId));
            albumCount.Set(await db.Albums.CountAsync(e => e.ArtistId == artistId));
            campaignCount.Set(await db.Campaigns.CountAsync(e => e.ArtistId == artistId));
            revenueEntryCount.Set(await db.RevenueEntries.CountAsync(e => e.ArtistId == artistId));
        }, [EffectTrigger.AfterInit(), refreshToken]);

        if (artist.Value == null) return null;

        var artistValue = artist.Value;

        void OnDelete()
        {
            showAlert("Are you sure you want to delete this artist?", result =>
            {
                if (result.IsOk())
                {
                    Delete(factory);
                    blades.Pop(refresh: true);
                }
            }, "Delete Artist", AlertButtonSet.OkCancel);
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
            .ToTrigger((isOpen) => new ArtistEditSheet(isOpen, refreshToken, artistId));

        var detailsCard = new Card(
            content: new
                {
                    artistValue.Id,
                    artistValue.Name
                }.ToDetails()
                .RemoveEmpty()
                .Builder(e => e.Id, e => e.CopyToClipboard()),
            footer: Layout.Horizontal().Gap(2).Align(Align.Right)
                    | dropDown
                    | editBtn
        ).Title("Artist Details").Width(Size.Units(100));

        var relatedCard = new Card(
            new List(
                new ListItem("Tracks", onClick: _ =>
                {
                    blades.Push(this, new ArtistTracksBlade(artistId), "Tracks");
                }, badge: trackCount.Value.ToString("N0")),
                new ListItem("Albums", onClick: _ =>
                {
                    blades.Push(this, new ArtistAlbumsBlade(artistId), "Albums");
                }, badge: albumCount.Value.ToString("N0")),
                new ListItem("Campaigns", onClick: _ =>
                {
                    blades.Push(this, new ArtistCampaignsBlade(artistId), "Campaigns");
                }, badge: campaignCount.Value.ToString("N0")),
                new ListItem("Revenue Entries", onClick: _ =>
                {
                    blades.Push(this, new ArtistRevenueEntriesBlade(artistId), "Revenue Entries");
                }, badge: revenueEntryCount.Value.ToString("N0"))
            ));

        return new Fragment()
               | (Layout.Vertical() | detailsCard | relatedCard)
               | alertView;
    }

    private void Delete(ArtistInsightToolContextFactory dbFactory)
    {
        using var db = dbFactory.CreateDbContext();
        var artist = db.Artists.FirstOrDefault(e => e.Id == artistId)!;
        db.Artists.Remove(artist);
        db.SaveChanges();
    }
}