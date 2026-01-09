namespace ArtistInsightTool.Apps.Views;

public class TrackCreateDialog(IState<bool> isOpen, RefreshToken refreshToken) : ViewBase
{
    private record TrackCreateRequest
    {
        [Required]
        public string Title { get; init; } = "";

        [Required]
        public int ArtistId { get; init; }

        public int? AlbumId { get; init; }

        public int? Duration { get; init; }
    }

    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var track = UseState(() => new TrackCreateRequest());

        UseEffect(() =>
        {
            var trackId = CreateTrack(factory, track.Value);
            refreshToken.Refresh(trackId);
        }, [track]);

        return track
            .ToForm()
            .Builder(e => e.ArtistId, e => e.ToAsyncSelectInput(QueryArtists(factory), LookupArtist(factory), placeholder: "Select Artist"))
            .Builder(e => e.AlbumId, e => e.ToAsyncSelectInput(QueryAlbums(factory), LookupAlbum(factory), placeholder: "Select Album"))
            .Builder(e => e.Duration, e => e.ToNumberInput())
            .ToDialog(isOpen, title: "Create Track", submitTitle: "Create");
    }

    private int CreateTrack(ArtistInsightToolContextFactory factory, TrackCreateRequest request)
    {
        using var db = factory.CreateDbContext();

        var track = new Track
        {
            Title = request.Title,
            ArtistId = request.ArtistId,
            AlbumId = request.AlbumId,
            Duration = request.Duration,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Tracks.Add(track);
        db.SaveChanges();

        return track.Id;
    }

    private static AsyncSelectQueryDelegate<int> QueryArtists(ArtistInsightToolContextFactory factory)
    {
        return async query =>
        {
            await using var db = factory.CreateDbContext();
            return (await db.Artists
                    .Where(e => e.Name.Contains(query))
                    .Select(e => new { e.Id, e.Name })
                    .Take(50)
                    .ToArrayAsync())
                .Select(e => new Option<int>(e.Name, e.Id))
                .ToArray();
        };
    }

    private static AsyncSelectLookupDelegate<int> LookupArtist(ArtistInsightToolContextFactory factory)
    {
        return async id =>
        {
            await using var db = factory.CreateDbContext();
            var artist = await db.Artists.FirstOrDefaultAsync(e => e.Id == id);
            if (artist == null) return null;
            return new Option<int>(artist.Name, artist.Id);
        };
    }

    private static AsyncSelectQueryDelegate<int?> QueryAlbums(ArtistInsightToolContextFactory factory)
    {
        return async query =>
        {
            await using var db = factory.CreateDbContext();
            return (await db.Albums
                    .Where(e => e.Title.Contains(query))
                    .Select(e => new { e.Id, e.Title })
                    .Take(50)
                    .ToArrayAsync())
                .Select(e => new Option<int?>(e.Title, e.Id))
                .ToArray();
        };
    }

    private static AsyncSelectLookupDelegate<int?> LookupAlbum(ArtistInsightToolContextFactory factory)
    {
        return async id =>
        {
            if (id == null) return null;
            await using var db = factory.CreateDbContext();
            var album = await db.Albums.FirstOrDefaultAsync(e => e.Id == id);
            if (album == null) return null;
            return new Option<int?>(album.Title, album.Id);
        };
    }
}