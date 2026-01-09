namespace ArtistInsightTool.Apps.Views;

public class RevenueEntryCreateDialog(IState<bool> isOpen, RefreshToken refreshToken) : ViewBase
{
  private record RevenueEntryCreateRequest
  {
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

    public int? CampaignId { get; init; }
  }

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var revenueEntry = UseState(() => new RevenueEntryCreateRequest());

    UseEffect(() =>
    {
      var revenueEntryId = CreateRevenueEntry(factory, revenueEntry.Value);
      refreshToken.Refresh(revenueEntryId);
    }, [revenueEntry]);

    return revenueEntry
        .ToForm()
        .Builder(e => e.SourceId, e => e.ToAsyncSelectInput(QuerySources(factory), LookupSource(factory), placeholder: "Select Source"))
        .Builder(e => e.Amount, e => e.ToMoneyInput().Currency("USD"))
        .Builder(e => e.RevenueDate, e => e.ToDateInput())
        .Builder(e => e.Description, e => e.ToTextAreaInput())
        .Builder(e => e.TrackId, e => e.ToAsyncSelectInput(QueryTracks(factory), LookupTrack(factory), placeholder: "Select Track"))
        .Builder(e => e.AlbumId, e => e.ToAsyncSelectInput(QueryAlbums(factory), LookupAlbum(factory), placeholder: "Select Album"))
        .Builder(e => e.CampaignId, e => e.ToAsyncSelectInput(QueryCampaigns(factory), LookupCampaign(factory), placeholder: "Select Campaign"))
        .ToDialog(isOpen, title: "Create Revenue Entry", submitTitle: "Create");
  }

  private int CreateRevenueEntry(ArtistInsightToolContextFactory factory, RevenueEntryCreateRequest request)
  {
    using var db = factory.CreateDbContext();

    var revenueEntry = new RevenueEntry
    {
      ArtistId = db.Artists.Select(a => a.Id).FirstOrDefault(),
      SourceId = request.SourceId,
      Amount = request.Amount,
      RevenueDate = request.RevenueDate,
      Description = request.Description,
      TrackId = request.TrackId,
      AlbumId = request.AlbumId,
      CampaignId = request.CampaignId,
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow
    };

    db.RevenueEntries.Add(revenueEntry);
    db.SaveChanges();

    return revenueEntry.Id;
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
      return campaign == null ? null : new Option<int?>(campaign.Name, campaign.Id);
    };
  }
}