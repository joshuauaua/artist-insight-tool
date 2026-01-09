namespace ArtistInsightTool.Apps.Views;

public class AlbumTracksCreateDialog(IState<bool> isOpen, RefreshToken refreshToken, int? albumId) : ViewBase
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
        var track = UseState(() => new TrackCreateRequest { AlbumId = albumId });

        UseEffect(() =>
        {
            if (albumId.HasValue)
            {
                var trackId = CreateTrack(factory, track.Value);
                refreshToken.Refresh(trackId);
            }
        }, [track]);

        return track
            .ToForm()
            .Builder(e => e.Title, e => e.ToTextInput())
            .Builder(e => e.ArtistId, e => e.ToAsyncSelectInput(QueryArtists(factory), LookupArtist(factory), placeholder: "Select Artist"))
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
}