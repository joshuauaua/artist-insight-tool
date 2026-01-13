using Ivy.Shared;

namespace ArtistInsightTool.Apps.Views;

public class RevenueTableView : ViewBase
{
  private record RevenueTableItem(int Id, DateTime RevenueDate, object DateDisplay, string Name, object NameDisplay, string Type, object TypeDisplay, string Campaign, object CampaignDisplay, decimal Amount, object AmountDisplay);

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    // var blades = UseService<IBladeController>(); // Removed Blade Dependency
    var refreshToken = this.UseRefreshToken();
    var allEntries = UseState<RevenueTableItem[]>([]);

    var searchQuery = UseState("");
    var sortField = UseState("Date");
    var sortDirection = UseState("Desc");
    // Filter states
    var selectedSource = UseState("All");

    // State for Details
    var selectedDetailsId = UseState<int?>(() => null);

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
         Layout.Horizontal().Width(120).Align(Align.Center).Add(r.RevenueDate.ToShortDateString()),
         r.Name,
         new Button(r.Name, () => selectedDetailsId.Set(r.Id)).Variant(ButtonVariant.Link),
         r.Type,
         Layout.Horizontal().Width(120).Add(r.Type),
         r.Campaign,
         Layout.Horizontal().Width(200).Add(r.Campaign),
         r.Amount,
         Layout.Horizontal().Width(100).Align(Align.Right).Add(r.Amount.ToString("C"))
     )).ToArray();

      allEntries.Set(tableData);
      return null;
    }

    UseEffect(LoadData, [EffectTrigger.AfterInit(), refreshToken]);

    // Handle "Sheet" Views


    if (selectedDetailsId.Value != null)
    {
      return new RevenueEditSheet(selectedDetailsId.Value.Value, () => selectedDetailsId.Set((int?)null));
    }

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

    if (selectedSource.Value != "All")
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
        .Add(p => p.DateDisplay)
        .Add(p => p.NameDisplay)
        .Add(p => p.TypeDisplay)
        .Add(p => p.CampaignDisplay)
        .Add(p => p.AmountDisplay)
        .Header(p => p.DateDisplay, "Date")
        .Header(p => p.NameDisplay, "Name")
        .Header(p => p.TypeDisplay, "Type")
        .Header(p => p.CampaignDisplay, "Campaign")
        .Header(p => p.AmountDisplay, "Amount")
        .Align(p => p.AmountDisplay, Align.Right)
        .Align(p => p.DateDisplay, Align.Center)
        .Empty("No entries match your search");

    var searchBar = searchQuery.ToTextInput()
        .Placeholder("Search streams...")
        .Width(300);

    var filterSelect = selectedSource.ToSelectInput(new List<Option<string>> {
        new("All", "All"),
        new("Streams", "Streams"),
        new("Merch", "Merch"),
        new("Live Show", "Live Show"),
        new("Sync", "Sync"),
        new("Others", "Others")
    });

    var headerContent = Layout.Vertical()
        .Width(Size.Full())
        .Gap(10)
        .Add(Layout.Horizontal().Add("Revenue Streams")) // Title
        .Add(Layout.Horizontal()
            .Width(Size.Full())
            .Gap(10)
            .Add(searchBar)
            .Add(filterSelect));
    // Removed Size.Full() from the inner container, so it hugs content.
    // But then it will be left aligned next to title.
    // We want it right aligned.
    // If Justify.SpaceBetween works...
    // Let's try putting them in a stack?
    // Or assume Size.Full() works if we give the first item a size?


    return Layout.Vertical()
        .Gap(10)
        .Padding(20)
        .Add(new Card(headerContent))
        .Add(new Card(table).Title(""));
  }
}
