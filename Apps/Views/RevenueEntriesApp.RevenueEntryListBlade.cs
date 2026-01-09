namespace ArtistInsightTool.Apps.Views;

public class RevenueEntryListBlade : ViewBase
{
  private record RevenueEntryListRecord(int Id, string ArtistName, string SourceDescription, decimal Amount, DateTime RevenueDate);

  public override object? Build()
  {
    var blades = UseContext<IBladeController>();
    var factory = UseService<ArtistInsightToolContextFactory>();
    var refreshToken = this.UseRefreshToken();

    UseEffect(() =>
    {
      if (refreshToken.ReturnValue is int revenueEntryId)
      {
        blades.Pop(this, true);
        blades.Push(this, new RevenueEntryDetailsBlade(revenueEntryId));
      }
    }, [refreshToken]);

    var onItemClicked = new Action<Event<ListItem>>(e =>
    {
      var revenueEntry = (RevenueEntryListRecord)e.Sender.Tag!;
      blades.Push(this, new RevenueEntryDetailsBlade(revenueEntry.Id), $"{revenueEntry.ArtistName} - {revenueEntry.SourceDescription}");
    });

    ListItem CreateItem(RevenueEntryListRecord record) =>
        new(title: $"{record.ArtistName} - {record.SourceDescription}", subtitle: $"{record.Amount:C} on {record.RevenueDate:yyyy-MM-dd}", onClick: onItemClicked, tag: record);

    var createBtn = Icons.Plus.ToButton(_ =>
    {
      blades.Pop(this);
    }).Ghost().Tooltip("Create Revenue Entry").ToTrigger((isOpen) => new RevenueEntryCreateDialog(isOpen, refreshToken));

    return new FilteredListView<RevenueEntryListRecord>(
        fetchRecords: (filter) => FetchRevenueEntries(factory, filter),
        createItem: CreateItem,
        toolButtons: createBtn,
        onFilterChanged: _ =>
        {
          blades.Pop(this);
        }
    );
  }

  private async Task<RevenueEntryListRecord[]> FetchRevenueEntries(ArtistInsightToolContextFactory factory, string filter)
  {
    await using var db = factory.CreateDbContext();

    var query = db.RevenueEntries
        .Include(e => e.Artist)
        .Include(e => e.Source)
        .AsQueryable();

    if (!string.IsNullOrWhiteSpace(filter))
    {
      filter = filter.Trim();
      query = query.Where(e => e.Artist.Name.Contains(filter) || e.Source.DescriptionText.Contains(filter));
    }

    return await query
        .OrderByDescending(e => e.RevenueDate)
        .Take(50)
        .Select(e => new RevenueEntryListRecord(
            e.Id,
            e.Artist.Name,
            e.Source.DescriptionText,
            e.Amount,
            e.RevenueDate
        ))
        .ToArrayAsync();
  }
}