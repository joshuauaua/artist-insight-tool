namespace ArtistInsightTool.Apps.Views;

public class AlbumTracksBlade(int? albumId) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var refreshToken = this.UseRefreshToken();
        var tracks = this.UseState<Track[]?>();
        var (alertView, showAlert) = this.UseAlert();

        this.UseEffect(async () =>
        {
            if (albumId == null) return;
            await using var db = factory.CreateDbContext();
            tracks.Set(await db.Tracks.Include(t => t.Artist).Where(t => t.AlbumId == albumId).ToArrayAsync());
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
                Artist = t.Artist.Name,
                Duration = t.Duration,
                _ = Layout.Horizontal().Gap(2)
                    | Icons.Ellipsis
                        .ToButton()
                        .Ghost()
                        .WithDropDown(MenuItem.Default("Delete").Icon(Icons.Trash).HandleSelect(OnDelete(t.Id)))
                    | Icons.Pencil
                        .ToButton()
                        .Outline()
                        .Tooltip("Edit")
                        .ToTrigger((isOpen) => new AlbumTracksEditSheet(isOpen, refreshToken, t.Id))
            })
            .ToTable()
            .RemoveEmptyColumns();

        var addBtn = new Button("Add Track").Icon(Icons.Plus).Outline()
            .ToTrigger((isOpen) => new AlbumTracksCreateDialog(isOpen, refreshToken, albumId));

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