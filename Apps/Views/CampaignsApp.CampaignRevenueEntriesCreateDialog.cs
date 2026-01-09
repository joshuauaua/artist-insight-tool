namespace ArtistInsightTool.Apps.Views;

public class CampaignRevenueEntriesCreateDialog(IState<bool> isOpen, RefreshToken refreshToken, int? campaignId) : ViewBase
{
    private record RevenueEntryCreateRequest
    {
        [Required]
        public int ArtistId { get; init; }

        [Required]
        public int SourceId { get; init; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Amount { get; init; }

        [Required]
        public DateTime RevenueDate { get; init; }

        public string? Description { get; init; }

        public int? TrackId { get; init; }

        public int? AlbumId { get; init; }
    }

    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var revenueEntry = UseState(() => new RevenueEntryCreateRequest
        {
            RevenueDate = DateTime.UtcNow
        });

        UseEffect(() =>
        {
            if (campaignId.HasValue)
            {
                CreateRevenueEntry(factory, revenueEntry.Value);
                refreshToken.Refresh();
            }
        }, [revenueEntry]);

        return revenueEntry
            .ToForm()
            .Builder(e => e.ArtistId, e => e.ToAsyncSelectInput(QueryArtists(factory), LookupArtist(factory), placeholder: "Select Artist"))
            .Builder(e => e.SourceId, e => e.ToAsyncSelectInput(QuerySources(factory), LookupSource(factory), placeholder: "Select Source"))
            .Builder(e => e.Amount, e => e.ToMoneyInput().Currency("USD"))
            .Builder(e => e.RevenueDate, e => e.ToDateInput())
            .Builder(e => e.Description, e => e.ToTextAreaInput())
            .Builder(e => e.TrackId, e => e.ToAsyncSelectInput(QueryTracks(factory), LookupTrack(factory), placeholder: "Select Track"))
            .Builder(e => e.AlbumId, e => e.ToAsyncSelectInput(QueryAlbums(factory), LookupAlbum(factory), placeholder: "Select Album"))
            .ToDialog(isOpen, title: "Create Revenue Entry", submitTitle: "Create");
    }

    private void CreateRevenueEntry(ArtistInsightToolContextFactory factory, RevenueEntryCreateRequest request)
    {
        using var db = factory.CreateDbContext();

        var revenueEntry = new RevenueEntry
        {
            ArtistId = request.ArtistId,
            SourceId = request.SourceId,
            Amount = request.Amount,
            RevenueDate = request.RevenueDate,
            Description = request.Description,
            TrackId = request.TrackId,
            AlbumId = request.AlbumId,
            CampaignId = campaignId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.RevenueEntries.Add(revenueEntry);
        db.SaveChanges();
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
            return artist == null ? null : new Option<int>(artist.Name, artist.Id);
        };
    }

    private static AsyncSelectQueryDelegate<int> QuerySources(ArtistInsightToolContextFactory factory)
    {
        return async query =>
        {
            await using var db = factory.CreateDbContext();
            return (await db.RevenueSources
                    .Where(e => e.DescriptionText.Contains(query))
                    .Select(e => new { e.Id, e.DescriptionText })
                    .Take(50)
                    .ToArrayAsync())
                .Select(e => new Option<int>(e.DescriptionText, e.Id))
                .ToArray();
        };
    }

    private static AsyncSelectLookupDelegate<int> LookupSource(ArtistInsightToolContextFactory factory)
    {
        return async id =>
        {
            await using var db = factory.CreateDbContext();
            var source = await db.RevenueSources.FirstOrDefaultAsync(e => e.Id == id);
            return source == null ? null : new Option<int>(source.DescriptionText, source.Id);
        };
    }

    private static AsyncSelectQueryDelegate<int?> QueryTracks(ArtistInsightToolContextFactory factory)
    {
        return async query =>
        {
            await using var db = factory.CreateDbContext();
            return (await db.Tracks
                    .Where(e => e.Title.Contains(query))
                    .Select(e => new { e.Id, e.Title })
                    .Take(50)
                    .ToArrayAsync())
                .Select(e => new Option<int?>(e.Title, e.Id))
                .ToArray();
        };
    }

    private static AsyncSelectLookupDelegate<int?> LookupTrack(ArtistInsightToolContextFactory factory)
    {
        return async id =>
        {
            if (id == null) return null;
            await using var db = factory.CreateDbContext();
            var track = await db.Tracks.FirstOrDefaultAsync(e => e.Id == id);
            return track == null ? null : new Option<int?>(track.Title, track.Id);
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
            return album == null ? null : new Option<int?>(album.Title, album.Id);
        };
    }
}