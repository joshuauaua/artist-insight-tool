namespace ArtistInsightTool.Apps.Views;

public class TrackDetailsBlade(int trackId) : ViewBase
{
    public override object? Build()
    {
        var factory = this.UseService<ArtistInsightToolContextFactory>();
        var blades = this.UseContext<IBladeController>();
        var refreshToken = this.UseRefreshToken();
        var track = this.UseState<Track?>();
        var revenueEntryCount = this.UseState<int>();
        var (alertView, showAlert) = this.UseAlert();

        this.UseEffect(async () =>
        {
            var db = factory.CreateDbContext();
            track.Set(await db.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .SingleOrDefaultAsync(t => t.Id == trackId));
            revenueEntryCount.Set(await db.RevenueEntries.CountAsync(re => re.TrackId == trackId));
        }, [EffectTrigger.AfterInit(), refreshToken]);

        if (track.Value == null) return null;

        var trackValue = track.Value;

        void OnDelete()
        {
            showAlert("Are you sure you want to delete this track?", result =>
            {
                if (result.IsOk())
                {
                    Delete(factory);
                    blades.Pop(refresh: true);
                }
            }, "Delete Track", AlertButtonSet.OkCancel);
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
            .ToTrigger((isOpen) => new TrackEditSheet(isOpen, refreshToken, trackId));

        var detailsCard = new Card(
            content: new
                {
                    trackValue.Id,
                    trackValue.Title,
                    ArtistName = trackValue.Artist.Name,
                    AlbumTitle = trackValue.Album?.Title ?? "N/A",
                    Duration = trackValue.Duration.HasValue ? $"{trackValue.Duration.Value} seconds" : "N/A"
                }.ToDetails()
                .RemoveEmpty()
                .Builder(e => e.Id, e => e.CopyToClipboard()),
            footer: Layout.Horizontal().Gap(2).Align(Align.Right)
                    | dropDown
                    | editBtn
        ).Title("Track Details").Width(Size.Units(100));

        var relatedCard = new Card(
            new List(
                new ListItem("Revenue Entries", onClick: _ =>
                {
                    blades.Push(this, new TrackRevenueEntriesBlade(trackId), "Revenue Entries");
                }, badge: revenueEntryCount.Value.ToString("N0"))
            ));

        return new Fragment()
               | (Layout.Vertical() | detailsCard | relatedCard)
               | alertView;
    }

    private void Delete(ArtistInsightToolContextFactory dbFactory)
    {
        using var db = dbFactory.CreateDbContext();
        var track = db.Tracks.FirstOrDefault(t => t.Id == trackId)!;
        db.Tracks.Remove(track);
        db.SaveChanges();
    }
}