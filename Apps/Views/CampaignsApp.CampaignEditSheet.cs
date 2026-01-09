namespace ArtistInsightTool.Apps.Views;

public class CampaignEditSheet(IState<bool> isOpen, RefreshToken refreshToken, int campaignId) : ViewBase
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
            .Builder(e => e.ArtistId, e => e.ToAsyncSelectInput(QueryArtists(factory), LookupArtist(factory), placeholder: "Select Artist"))
            .Remove(e => e.Id, e => e.CreatedAt, e => e.UpdatedAt)
            .ToSheet(isOpen, "Edit Campaign");
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