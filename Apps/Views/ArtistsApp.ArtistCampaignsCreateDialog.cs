namespace ArtistInsightTool.Apps.Views;

public class ArtistCampaignsCreateDialog(IState<bool> isOpen, RefreshToken refreshToken, int artistId) : ViewBase
{
    private record CampaignCreateRequest
    {
        [Required]
        public string Name { get; init; } = "";

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
            .Builder(e => e.StartDate, e => e.ToDateInput())
            .Builder(e => e.EndDate, e => e.ToDateInput())
            .ToDialog(isOpen, title: "Create Campaign", submitTitle: "Create");
    }

    private int CreateCampaign(ArtistInsightToolContextFactory factory, CampaignCreateRequest request)
    {
        using var db = factory.CreateDbContext();

        var campaign = new Campaign
        {
            ArtistId = artistId,
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Campaigns.Add(campaign);
        db.SaveChanges();

        return campaign.Id;
    }
}