namespace ArtistInsightTool.Apps.Views;

public class RevenueTableView : ViewBase
{
  private record RevenueEntryTableRecord(int Id, string ArtistName, string SourceDescription, decimal Amount, DateTime RevenueDate);

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var refreshToken = this.UseRefreshToken();
    var entries = UseState<RevenueEntryTableRecord[]>([]);

    async Task<IDisposable?> LoadData()
    {
      await using var db = factory.CreateDbContext();
      var data = await db.RevenueEntries
         .Include(e => e.Artist)
         .Include(e => e.Source)
         .OrderByDescending(e => e.RevenueDate)
         .Take(50)
         .Select(e => new RevenueEntryTableRecord(
             e.Id,
             e.Artist.Name,
             e.Source.DescriptionText,
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
        .Add(p => p.ArtistName)
        .Add(p => p.SourceDescription)
        .Add(p => p.Amount)
        .Add(p => p.RevenueDate)
        .Header(p => p.ArtistName, "Artist")
        .Header(p => p.SourceDescription, "Source")
        .Header(p => p.Amount, "Amount")
        .Header(p => p.RevenueDate, "Date")
        .Align(p => p.Amount, Align.Right)
        .Align(p => p.RevenueDate, Align.Center)
        .Empty("No entries found");

    return new Card(table)
        .Title("Revenue Table");
  }
}
