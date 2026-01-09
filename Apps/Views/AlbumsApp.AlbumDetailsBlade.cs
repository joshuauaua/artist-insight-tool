namespace ArtistInsightTool.Apps.Views;

public class AlbumDetailsBlade(int albumId) : ViewBase
{
    public override object? Build()
    {
        var factory = this.UseService<ArtistInsightToolContextFactory>();
        var blades = this.UseContext<IBladeController>();
        var refreshToken = this.UseRefreshToken();
        var album = this.UseState<Album?>();
        var trackCount = this.UseState<int>();
        var revenueEntryCount = this.UseState<int>();
        var (alertView, showAlert) = this.UseAlert();

        this.UseEffect(async () =>
        {
            var db = factory.CreateDbContext();
            album.Set(await db.Albums
                .Include(e => e.Artist)
                .SingleOrDefaultAsync(e => e.Id == albumId));
            trackCount.Set(await db.Tracks.CountAsync(e => e.AlbumId == albumId));
            revenueEntryCount.Set(await db.RevenueEntries.CountAsync(e => e.AlbumId == albumId));
        }, [EffectTrigger.AfterInit(), refreshToken]);

        if (album.Value == null) return null;

        var albumValue = album.Value;

        void OnDelete()
        {
            showAlert("Are you sure you want to delete this album?", result =>
            {
                if (result.IsOk())
                {
                    Delete(factory);
                    blades.Pop(refresh: true);
                }
            }, "Delete Album", AlertButtonSet.OkCancel);
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
            .ToTrigger((isOpen) => new AlbumEditSheet(isOpen, refreshToken, albumId));

        var detailsCard = new Card(
            content: new
                {
                    albumValue.Id,
                    albumValue.Title,
                    ArtistName = albumValue.Artist.Name,
                    ReleaseDate = albumValue.ReleaseDate?.ToString("yyyy-MM-dd") ?? "N/A"
                }.ToDetails()
                .RemoveEmpty()
                .Builder(e => e.Id, e => e.CopyToClipboard()),
            footer: Layout.Horizontal().Gap(2).Align(Align.Right)
                    | dropDown
                    | editBtn
        ).Title("Album Details").Width(Size.Units(100));

        var relatedCard = new Card(
            new List(
                new ListItem("Tracks", onClick: _ =>
                {
                    blades.Push(this, new AlbumTracksBlade(albumId), "Tracks");
                }, badge: trackCount.Value.ToString("N0")),
                new ListItem("Revenue Entries", onClick: _ =>
                {
                    blades.Push(this, new AlbumRevenueEntriesBlade(albumId), "Revenue Entries");
                }, badge: revenueEntryCount.Value.ToString("N0"))
            ));

        return new Fragment()
               | (Layout.Vertical() | detailsCard | relatedCard)
               | alertView;
    }

    private void Delete(ArtistInsightToolContextFactory dbFactory)
    {
        using var db = dbFactory.CreateDbContext();
        var album = db.Albums.FirstOrDefault(e => e.Id == albumId)!;
        db.Albums.Remove(album);
        db.SaveChanges();
    }
}