namespace ArtistInsightTool.Apps.Views;

public class ArtistTracksBlade(int artistId) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var refreshToken = this.UseRefreshToken();
        var tracks = this.UseState<Track[]?>();
        var (alertView, showAlert) = this.UseAlert();

        this.UseEffect(async () =>
        {
            await using var db = factory.CreateDbContext();
            tracks.Set(await db.Tracks.Include(t => t.Album).Where(t => t.ArtistId == artistId).ToArrayAsync());
        }, [ EffectTrigger.AfterInit(), refreshToken ]);

        Action OnDelete(int id)
        {
            return () =>
            {
                showAlert("Are you sure you want to delete this track?", result =>
                {
                    if (result.IsOk())
                    {
                        Delete(factory, id);
                        refreshToken.Refresh();
                    }
                }, "Delete Track", AlertButtonSet.OkCancel);
            };
        }

        if (tracks.Value == null) return null;

        var table = tracks.Value.Select(t => new
            {
                Title = t.Title,
                Album = t.Album?.Title ?? "Unknown",
                Duration = t.Duration,
                _ = Layout.Horizontal().Gap(2)
                    | Icons.Ellipsis
                        .ToButton()
                        .Ghost()
                        .WithDropDown(MenuItem.Default("Delete").Icon(Icons.Trash).HandleSelect(OnDelete(t.Id)))
                    | Icons.ChevronRight
                        .ToButton()
                        .Outline()
                        .Tooltip("Edit")
                        .ToTrigger((isOpen) => new ArtistTracksEditSheet(isOpen, refreshToken, t.Id))
            })
            .ToTable()
            .RemoveEmptyColumns();

        var addBtn = new Button("Add Track").Icon(Icons.Plus).Outline()
            .ToTrigger((isOpen) => new ArtistTracksCreateDialog(isOpen, refreshToken, artistId));

        return new Fragment()
               | BladeHelper.WithHeader(addBtn, table)
               | alertView;
    }

    public void Delete(ArtistInsightToolContextFactory factory, int trackId)
    {
        using var db = factory.CreateDbContext();
        db.Tracks.Remove(db.Tracks.Single(t => t.Id == trackId));
        db.SaveChanges();
    }
}