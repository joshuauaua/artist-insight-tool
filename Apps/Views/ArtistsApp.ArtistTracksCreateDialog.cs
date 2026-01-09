namespace ArtistInsightTool.Apps.Views;

public class ArtistTracksCreateDialog(IState<bool> isOpen, RefreshToken refreshToken, int artistId) : ViewBase
{
    private record TrackCreateRequest
    {
        [Required]
        public string Title { get; init; } = "";

        [Required]
        public int? AlbumId { get; init; } = null;

        public int? Duration { get; init; } = null;
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
            .Builder(e => e.AlbumId, e => e.ToAsyncSelectInput(QueryAlbums(factory, artistId), LookupAlbum(factory)))
            .Builder(e => e.Duration, e => e.ToNumberInput())
            .ToDialog(isOpen, title: "Create Track", submitTitle: "Create");
    }

    private int CreateTrack(ArtistInsightToolContextFactory factory, TrackCreateRequest request)
    {
        using var db = factory.CreateDbContext();

        var track = new Track
        {
            ArtistId = artistId,
            Title = request.Title,
            AlbumId = request.AlbumId,
            Duration = request.Duration,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Tracks.Add(track);
        db.SaveChanges();

        return track.Id;
    }

    private static AsyncSelectQueryDelegate<int?> QueryAlbums(ArtistInsightToolContextFactory factory, int artistId)
    {
        return async query =>
        {
            await using var db = factory.CreateDbContext();
            return (await db.Albums
                    .Where(e => e.ArtistId == artistId && e.Title.Contains(query))
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