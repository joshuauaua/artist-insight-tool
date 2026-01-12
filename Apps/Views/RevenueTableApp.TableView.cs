namespace ArtistInsightTool.Apps.Views;

public class RevenueTableView : ViewBase
{
  private record RevenueTableItem(int Id, DateTime RevenueDate, string Name, string Type, string Campaign, decimal Amount, object Details);

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var blades = UseService<IBladeController>();
    var refreshToken = this.UseRefreshToken();
    var allEntries = UseState<RevenueTableItem[]>([]);

    var searchQuery = UseState("");
    var sortField = UseState("Date");
    var sortDirection = UseState("Desc");
    // Filter states - could be more complex but let's start simple with Source filtering
    var selectedSource = UseState<string?>(() => null);

    async Task<IDisposable?> LoadData()
    {
      await using var db = factory.CreateDbContext();
      // Increase limit to enable better client-side search/filter
      var rawData = await db.RevenueEntries
         .Include(e => e.Track).ThenInclude(t => t.Album)
         .Include(e => e.Album)
         .Include(e => e.Source)
         .OrderByDescending(e => e.RevenueDate)
         .Take(1000)
         .Select(e => new
         {
           e.Id,
           e.RevenueDate,
           Name = e.Track != null ? e.Track.Title : (e.Album != null ? e.Album.Title : (e.Description ?? "-")),
           Type = e.Source.DescriptionText,
           Campaign = e.Track != null && e.Track.Album != null ? ($"{e.Track.Album.ReleaseType}: {e.Track.Album.Title}") : (e.Album != null ? ($"{e.Album.ReleaseType}: {e.Album.Title}") : "-"),
           e.Amount
         })
         .ToArrayAsync();

      var tableData = rawData.Select(r => new RevenueTableItem(
          r.Id,
          r.RevenueDate,
          r.Name,
          r.Type,
          r.Campaign,
          r.Amount,
          new Button("Details", () => blades.Push(new RevenueDetailsBlade(r.Id)))
      )).ToArray();

      allEntries.Set(tableData);
      return null;
    }

    UseEffect(LoadData, [EffectTrigger.AfterInit(), refreshToken]);

    // Apply Filters and Search
    var filteredEntries = allEntries.Value;

    if (!string.IsNullOrWhiteSpace(searchQuery.Value))
    {
      var q = searchQuery.Value.ToLowerInvariant();
      filteredEntries = filteredEntries.Where(e =>
          e.Name.ToLowerInvariant().Contains(q) ||
          e.Campaign.ToLowerInvariant().Contains(q) ||
          e.Type.ToLowerInvariant().Contains(q)
      ).ToArray();
    }

    if (selectedSource.Value != null)
    {
      filteredEntries = filteredEntries.Where(e => e.Type == selectedSource.Value).ToArray();
    }

    // Apply Sorting
    filteredEntries = (sortField.Value, sortDirection.Value) switch
    {
      ("Date", "Desc") => filteredEntries.OrderByDescending(e => e.RevenueDate).ToArray(),
      ("Date", "Asc") => filteredEntries.OrderBy(e => e.RevenueDate).ToArray(),
      ("Amount", "Desc") => filteredEntries.OrderByDescending(e => e.Amount).ToArray(),
      ("Amount", "Asc") => filteredEntries.OrderBy(e => e.Amount).ToArray(),
      ("Name", "Asc") => filteredEntries.OrderBy(e => e.Name).ToArray(),
      ("Name", "Desc") => filteredEntries.OrderByDescending(e => e.Name).ToArray(),
      ("Type", "Asc") => filteredEntries.OrderBy(e => e.Type).ToArray(),
      ("Type", "Desc") => filteredEntries.OrderByDescending(e => e.Type).ToArray(),
      ("Campaign", "Asc") => filteredEntries.OrderBy(e => e.Campaign).ToArray(),
      ("Campaign", "Desc") => filteredEntries.OrderByDescending(e => e.Campaign).ToArray(),
      _ => filteredEntries
    };

    var table = filteredEntries.ToTable()
        .Width(Size.Full())
        .Clear()
        .Add(p => p.RevenueDate)
        .Add(p => p.Name)
        .Add(p => p.Type)
        .Add(p => p.Campaign)
        .Add(p => p.Amount)
        .Add(p => p.Details)
        .Header(p => p.RevenueDate, "Date")
        .Header(p => p.Name, "Name")
        .Header(p => p.Type, "Type")
        .Header(p => p.Campaign, "Campaign")
        .Header(p => p.Amount, "Amount")
        .Header(p => p.Details, "")
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
