namespace ArtistInsightTool.Apps.Views;

public class RevenueTableView : ViewBase
{
  private record RevenueEntryTableRecord(int Id, string SourceDescription, string ReleaseTitle, string Type, decimal Amount, DateTime RevenueDate);

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var refreshToken = this.UseRefreshToken();
    var entries = UseState<RevenueEntryTableRecord[]>([]);

    async Task<IDisposable?> LoadData()
    {
      await using var db = factory.CreateDbContext();
      var data = await db.RevenueEntries
         .Include(e => e.Track).ThenInclude(t => t.Album)
         .Include(e => e.Album)
         .OrderByDescending(e => e.RevenueDate)
         .Take(50)
         .Select(e => new RevenueEntryTableRecord(
             e.Id,
             e.Source.DescriptionText,
             e.Track != null ? e.Track.Title : (e.Album != null ? e.Album.Title : "-"),
             e.Track != null ? (e.Track.Album != null ? e.Track.Album.ReleaseType : "Single") : (e.Album != null ? e.Album.ReleaseType : "-"),
             e.Amount,
             e.RevenueDate
         ))
         .ToArrayAsync();
      entries.Set(data);
      return null;
    }

    UseEffect(LoadData, [EffectTrigger.AfterInit(), refreshToken]);

    var table = entries.Value.ToTable()
        .Width(Size.Full())
        .Clear()
        .Add(p => p.SourceDescription)
        .Add(p => p.ReleaseTitle)
        .Add(p => p.Type)
        .Add(p => p.Amount)
        .Add(p => p.RevenueDate)
        .Header(p => p.SourceDescription, "Source")
        .Header(p => p.ReleaseTitle, "Release")
        .Header(p => p.Type, "Type")
        .Header(p => p.Amount, "Amount")
        .Header(p => p.RevenueDate, "Date")
        .Align(p => p.Amount, Align.Right)
        .Align(p => p.RevenueDate, Align.Center)
        .Empty("No entries found");

    return new Card(table)
        .Title("Revenue Table");
  }
}
