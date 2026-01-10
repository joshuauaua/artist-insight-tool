namespace ArtistInsightTool.Apps.Views;

public class RevenueTableView : ViewBase
{
  private record RevenueEntryTableRecord(int Id, string SourceDescription, string ReleaseTitle, string Type, decimal Amount, DateTime RevenueDate);

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var refreshToken = this.UseRefreshToken();
    var allEntries = UseState<RevenueEntryTableRecord[]>([]);

    var searchQuery = UseState("");
    var sortField = UseState("Date");
    var sortDirection = UseState("Desc");
    // Filter states - could be more complex but let's start simple with Source filtering
    var selectedSource = UseState<string?>(() => null);

    async Task<IDisposable?> LoadData()
    {
      await using var db = factory.CreateDbContext();
      // Increase limit to enable better client-side search/filter
      var data = await db.RevenueEntries
         .Include(e => e.Track).ThenInclude(t => t.Album)
         .Include(e => e.Album)
         .Include(e => e.Source)
         .OrderByDescending(e => e.RevenueDate)
         .Take(1000)
         .Select(e => new RevenueEntryTableRecord(
             e.Id,
             e.Source.DescriptionText,
             e.Track != null ? e.Track.Title : (e.Album != null ? e.Album.Title : "-"),
             e.Track != null ? (e.Track.Album != null ? e.Track.Album.ReleaseType : "Single") : (e.Album != null ? e.Album.ReleaseType : "-"),
             e.Amount,
             e.RevenueDate
         ))
         .ToArrayAsync();
      allEntries.Set(data);
      return null;
    }

    UseEffect(LoadData, [EffectTrigger.AfterInit(), refreshToken]);

    // Apply Filters and Search
    var filteredEntries = allEntries.Value;

    if (!string.IsNullOrWhiteSpace(searchQuery.Value))
    {
      var q = searchQuery.Value.ToLowerInvariant();
      filteredEntries = filteredEntries.Where(e =>
          e.SourceDescription.ToLowerInvariant().Contains(q) ||
          e.ReleaseTitle.ToLowerInvariant().Contains(q) ||
          e.Type.ToLowerInvariant().Contains(q)
      ).ToArray();
    }

    if (selectedSource.Value != null)
    {
      filteredEntries = filteredEntries.Where(e => e.SourceDescription == selectedSource.Value).ToArray();
    }

    // Apply Sorting
    filteredEntries = (sortField.Value, sortDirection.Value) switch
    {
      ("Date", "Desc") => filteredEntries.OrderByDescending(e => e.RevenueDate).ToArray(),
      ("Date", "Asc") => filteredEntries.OrderBy(e => e.RevenueDate).ToArray(),
      ("Amount", "Desc") => filteredEntries.OrderByDescending(e => e.Amount).ToArray(),
      ("Amount", "Asc") => filteredEntries.OrderBy(e => e.Amount).ToArray(),
      ("Source", "Asc") => filteredEntries.OrderBy(e => e.SourceDescription).ToArray(),
      ("Source", "Desc") => filteredEntries.OrderByDescending(e => e.SourceDescription).ToArray(),
      _ => filteredEntries
    };

    var table = filteredEntries.ToTable()
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
        .Empty("No entries match your search");

    var searchBar = searchQuery.ToTextInput()
        .Placeholder("Search streams...")
        .Width(Size.Full());

    // Simplified Filter Buttons
    var filterRow = Layout.Horizontal()
        .Gap(5)
        .Add(new Button("All"))
        .Add(new Button("Streams"))
        .Add(new Button("Merch"));

    // Actions button (Sort placeholder for now - or simple toggle)
    var sortButton = new Button("Sort: " + sortField.Value);

    var header = Layout.Horizontal()
        .Align(Align.Center)
        .Gap(20)
        .Add("Revenue Streams")
        .Add(sortButton);

    return new Card(
        Layout.Vertical()
            .Gap(15)
            .Add(header)
            .Add(searchBar)
            .Add(filterRow)
            .Add(table)
    ).Title(""); // Empty card title as we implement custom header
  }
}
