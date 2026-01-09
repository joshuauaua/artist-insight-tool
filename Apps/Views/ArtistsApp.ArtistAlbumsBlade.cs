namespace ArtistInsightTool.Apps.Views;

public class ArtistAlbumsBlade(int artistId) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var refreshToken = this.UseRefreshToken();
        var albums = this.UseState<Album[]?>();
        var (alertView, showAlert) = this.UseAlert();

        this.UseEffect(async () =>
        {
            await using var db = factory.CreateDbContext();
            albums.Set(await db.Albums.Where(a => a.ArtistId == artistId).ToArrayAsync());
        }, [ EffectTrigger.AfterInit(), refreshToken ]);

        Action OnDelete(int id)
        {
            return () =>
            {
                showAlert("Are you sure you want to delete this album?", result =>
                {
                    if (result.IsOk())
                    {
                        Delete(factory, id);
                        refreshToken.Refresh();
                    }
                }, "Delete Album", AlertButtonSet.OkCancel);
            };
        }

        if (albums.Value == null) return null;

        var table = albums.Value.Select(a => new
            {
                Title = a.Title,
                ReleaseDate = a.ReleaseDate,
                _ = Layout.Horizontal().Gap(2)
                    | Icons.Ellipsis
                        .ToButton()
                        .Ghost()
                        .WithDropDown(MenuItem.Default("Delete").Icon(Icons.Trash).HandleSelect(OnDelete(a.Id)))
                    | Icons.ChevronRight
                        .ToButton()
                        .Outline()
                        .Tooltip("Edit")
                        .ToTrigger((isOpen) => new ArtistAlbumsEditSheet(isOpen, refreshToken, a.Id))
            })
            .ToTable()
            .RemoveEmptyColumns();

        var addBtn = new Button("Add Album").Icon(Icons.Plus).Outline()
            .ToTrigger((isOpen) => new ArtistAlbumsCreateDialog(isOpen, refreshToken, artistId));

        return new Fragment()
               | BladeHelper.WithHeader(addBtn, table)
               | alertView;
    }

    public void Delete(ArtistInsightToolContextFactory factory, int albumId)
    {
        using var db = factory.CreateDbContext();
        db.Albums.Remove(db.Albums.Single(a => a.Id == albumId));
        db.SaveChanges();
    }
}