namespace ArtistInsightTool.Apps.Views;

public class CampaignCreateDialog(IState<bool> isOpen, RefreshToken refreshToken) : ViewBase
{
    private record CampaignCreateRequest
    {
        [Required]
        public string Name { get; init; } = "";

        [Required]
        public int? ArtistId { get; init; } = null;

        public DateTime? StartDate { get; init; } = null;

        public DateTime? EndDate { get; init; } = null;
    }

    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var campaign = UseState(() => new CampaignCreateRequest());

        UseEffect(() =>
        {
            var campaignId = CreateCampaign(factory, campaign.Value);
            refreshToken.Refresh(campaignId);
        }, [campaign]);

        return campaign
            .ToForm()
            .Builder(e => e.ArtistId, e => e.ToAsyncSelectInput(QueryArtists(factory), LookupArtist(factory), placeholder: "Select Artist"))
            .Builder(e => e.StartDate, e => e.ToDateInput())
            .Builder(e => e.EndDate, e => e.ToDateInput())
            .ToDialog(isOpen, title: "Create Campaign", submitTitle: "Create");
    }

    private int CreateCampaign(ArtistInsightToolContextFactory factory, CampaignCreateRequest request)
    {
        using var db = factory.CreateDbContext();

        var campaign = new Campaign
        {
            Name = request.Name,
            ArtistId = request.ArtistId!.Value,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Campaigns.Add(campaign);
        db.SaveChanges();

        return campaign.Id;
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