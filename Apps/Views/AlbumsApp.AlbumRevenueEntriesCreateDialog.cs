namespace ArtistInsightTool.Apps.Views;

public class AlbumRevenueEntriesCreateDialog(IState<bool> isOpen, RefreshToken refreshToken, int? albumId) : ViewBase
{
    private record RevenueEntryCreateRequest
    {
        [Required]
        public int ArtistId { get; init; }

        [Required]
        public int SourceId { get; init; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; init; }

        [Required]
        public DateTime RevenueDate { get; init; } = DateTime.UtcNow;

        public string? Description { get; init; }
    }

    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var revenueEntry = UseState(() => new RevenueEntryCreateRequest());

        UseEffect(() =>
        {
            if (albumId.HasValue)
            {
                var entryId = CreateRevenueEntry(factory, revenueEntry.Value, albumId.Value);
                refreshToken.Refresh(entryId);
            }
        }, [revenueEntry]);

        return revenueEntry
            .ToForm()
            .Builder(e => e.ArtistId, e => e.ToAsyncSelectInput(QueryArtists(factory), LookupArtist(factory), placeholder: "Select Artist"))
            .Builder(e => e.SourceId, e => e.ToAsyncSelectInput(QuerySources(factory), LookupSource(factory), placeholder: "Select Source"))
            .Builder(e => e.Amount, e => e.ToMoneyInput().Currency("USD"))
            .Builder(e => e.RevenueDate, e => e.ToDateInput())
            .Builder(e => e.Description, e => e.ToTextAreaInput())
            .ToDialog(isOpen, title: "Create Revenue Entry", submitTitle: "Create");
    }

    private int CreateRevenueEntry(ArtistInsightToolContextFactory factory, RevenueEntryCreateRequest request, int albumId)
    {
        using var db = factory.CreateDbContext();

        var revenueEntry = new RevenueEntry
        {
            ArtistId = request.ArtistId,
            SourceId = request.SourceId,
            Amount = request.Amount,
            RevenueDate = request.RevenueDate,
            Description = request.Description,
            AlbumId = albumId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.RevenueEntries.Add(revenueEntry);
        db.SaveChanges();

        return revenueEntry.Id;
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
            if (source == null) return null;
            return new Option<int>(source.DescriptionText, source.Id);
        };
    }
}