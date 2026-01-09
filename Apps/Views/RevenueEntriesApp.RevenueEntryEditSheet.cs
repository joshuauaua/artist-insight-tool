namespace ArtistInsightTool.Apps.Views;

public class RevenueEntryEditSheet(IState<bool> isOpen, RefreshToken refreshToken, int revenueEntryId) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var revenueEntry = UseState(() => factory.CreateDbContext().RevenueEntries.FirstOrDefault(e => e.Id == revenueEntryId)!);

        UseEffect(() =>
        {
            using var db = factory.CreateDbContext();
            revenueEntry.Value.UpdatedAt = DateTime.UtcNow;
            db.RevenueEntries.Update(revenueEntry.Value);
            db.SaveChanges();
            refreshToken.Refresh();
        }, [revenueEntry]);

        return revenueEntry
            .ToForm()
            .Builder(e => e.Amount, e => e.ToMoneyInput().Currency("USD"))
            .Builder(e => e.RevenueDate, e => e.ToDateInput())
            .Builder(e => e.Description, e => e.ToTextAreaInput())
            .Builder(e => e.ArtistId, e => e.ToAsyncSelectInput(QueryArtists(factory), LookupArtist(factory), placeholder: "Select Artist"))
            .Builder(e => e.SourceId, e => e.ToAsyncSelectInput(QuerySources(factory), LookupSource(factory), placeholder: "Select Revenue Source"))
            .Builder(e => e.TrackId, e => e.ToAsyncSelectInput(QueryTracks(factory), LookupTrack(factory), placeholder: "Select Track"))
            .Builder(e => e.AlbumId, e => e.ToAsyncSelectInput(QueryAlbums(factory), LookupAlbum(factory), placeholder: "Select Album"))
            .Builder(e => e.CampaignId, e => e.ToAsyncSelectInput(QueryCampaigns(factory), LookupCampaign(factory), placeholder: "Select Campaign"))
            .Remove(e => e.Id, e => e.CreatedAt, e => e.UpdatedAt)
            .ToSheet(isOpen, "Edit Revenue Entry");
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

    private static AsyncSelectQueryDelegate<int?> QuerySources(ArtistInsightToolContextFactory factory)
    {
        return async query =>
        {
            await using var db = factory.CreateDbContext();
            return (await db.RevenueSources
                    .Where(e => e.DescriptionText.Contains(query))
                    .Select(e => new { e.Id, e.DescriptionText })
                    .Take(50)
                    .ToArrayAsync())
                .Select(e => new Option<int?>(e.DescriptionText, e.Id))
                .ToArray();
        };
    }

    private static AsyncSelectLookupDelegate<int?> LookupSource(ArtistInsightToolContextFactory factory)
    {
        return async id =>
        {
            if (id == null) return null;
            await using var db = factory.CreateDbContext();
            var source = await db.RevenueSources.FirstOrDefaultAsync(e => e.Id == id);
            if (source == null) return null;
            return new Option<int?>(source.DescriptionText, source.Id);
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
            if (track == null) return null;
            return new Option<int?>(track.Title, track.Id);
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

    private static AsyncSelectQueryDelegate<int?> QueryCampaigns(ArtistInsightToolContextFactory factory)
    {
        return async query =>
        {
            await using var db = factory.CreateDbContext();
            return (await db.Campaigns
                    .Where(e => e.Name.Contains(query))
                    .Select(e => new { e.Id, e.Name })
                    .Take(50)
                    .ToArrayAsync())
                .Select(e => new Option<int?>(e.Name, e.Id))
                .ToArray();
        };
    }

    private static AsyncSelectLookupDelegate<int?> LookupCampaign(ArtistInsightToolContextFactory factory)
    {
        return async id =>
        {
            if (id == null) return null;
            await using var db = factory.CreateDbContext();
            var campaign = await db.Campaigns.FirstOrDefaultAsync(e => e.Id == id);
            if (campaign == null) return null;
            return new Option<int?>(campaign.Name, campaign.Id);
        };
    }
}