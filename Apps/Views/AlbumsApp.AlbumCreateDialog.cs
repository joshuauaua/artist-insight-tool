namespace ArtistInsightTool.Apps.Views;

public class AlbumCreateDialog(IState<bool> isOpen, RefreshToken refreshToken) : ViewBase
{
    private record AlbumCreateRequest
    {
        [Required]
        public string Title { get; init; } = "";

        [Required]
        public int? ArtistId { get; init; } = null;

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
            .Builder(e => e.ArtistId, e => e.ToAsyncSelectInput(QueryArtists(factory), LookupArtist(factory), placeholder: "Select Artist"))
            .Builder(e => e.ReleaseDate, e => e.ToDateInput())
            .ToDialog(isOpen, title: "Create Album", submitTitle: "Create");
    }

    private int CreateAlbum(ArtistInsightToolContextFactory factory, AlbumCreateRequest request)
    {
        using var db = factory.CreateDbContext();

        var album = new Album
        {
            Title = request.Title,
            ArtistId = request.ArtistId!.Value,
            ReleaseDate = request.ReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Albums.Add(album);
        db.SaveChanges();

        return album.Id;
    }

    private static AsyncSelectQueryDelegate<int?> QueryArtists(ArtistInsightToolContextFactory factory)
    {
        return async query =>
        {
            await using var db = factory.CreateDbContext();
            return (await db.Artists
                    .Where(e => e.Name.Contains(query))
                    .Select(e => new { e.Id, e.Name })
                    .Take(50)
                    .ToArrayAsync())
                .Select(e => new Option<int?>(e.Name, e.Id))
                .ToArray();
        };
    }

    private static AsyncSelectLookupDelegate<int?> LookupArtist(ArtistInsightToolContextFactory factory)
    {
        return async id =>
        {
            if (id == null) return null;
            await using var db = factory.CreateDbContext();
            var artist = await db.Artists.FirstOrDefaultAsync(e => e.Id == id);
            if (artist == null) return null;
            return new Option<int?>(artist.Name, artist.Id);
        };
    }
}