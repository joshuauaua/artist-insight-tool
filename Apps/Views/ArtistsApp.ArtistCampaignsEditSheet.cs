namespace ArtistInsightTool.Apps.Views;

public class ArtistCampaignsEditSheet(IState<bool> isOpen, RefreshToken refreshToken, int campaignId) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var campaign = UseState(() => factory.CreateDbContext().Campaigns.FirstOrDefault(e => e.Id == campaignId)!);

        UseEffect(() =>
        {
            using var db = factory.CreateDbContext();
            campaign.Value.UpdatedAt = DateTime.UtcNow;
            db.Campaigns.Update(campaign.Value);
            db.SaveChanges();
            refreshToken.Refresh();
        }, [campaign]);

        return campaign
            .ToForm()
            .Builder(e => e.Name, e => e.ToTextInput())
            .Builder(e => e.StartDate, e => e.ToDateInput())
            .Builder(e => e.EndDate, e => e.ToDateInput())
            .Remove(e => e.Id, e => e.CreatedAt, e => e.UpdatedAt)
            .ToSheet(isOpen, "Edit Campaign");
    }
}