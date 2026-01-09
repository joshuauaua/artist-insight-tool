namespace ArtistInsightTool.Apps.Views;

public class ArtistAlbumsCreateDialog(IState<bool> isOpen, RefreshToken refreshToken, int artistId) : ViewBase
{
    private record AlbumCreateRequest
    {
        [Required]
        public string Title { get; init; } = "";

        public DateTime? ReleaseDate { get; init; } = null;
    }

    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var album = UseState(() => new AlbumCreateRequest());

        UseEffect(() =>
        {
            var albumId = CreateAlbum(factory, album.Value);
            refreshToken.Refresh(albumId);
        }, [album]);

        return album
            .ToForm()
            .Builder(e => e.ReleaseDate, e => e.ToDateInput())
            .ToDialog(isOpen, title: "Create Album", submitTitle: "Create");
    }

    private int CreateAlbum(ArtistInsightToolContextFactory factory, AlbumCreateRequest request)
    {
        using var db = factory.CreateDbContext();

        var album = new Album
        {
            ArtistId = artistId,
            Title = request.Title,
            ReleaseDate = request.ReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Albums.Add(album);
        db.SaveChanges();

        return album.Id;
    }
}