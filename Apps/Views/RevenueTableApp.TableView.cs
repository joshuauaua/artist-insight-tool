using Ivy.Shared;

namespace ArtistInsightTool.Apps.Views;

public class RevenueTableView : ViewBase
{
  private record RevenueTableItem(int Id, object DateDisplay, object NameDisplay, object TypeDisplay, object SourceDisplay, object AmountDisplay, DateTime RevenueDate, string Name, string Type, string Source, decimal Amount);

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

           e.Amount,
           Source = e.Integration ?? "Manual"
         })
         .ToArrayAsync();

      var tableData = rawData.Select(r => new RevenueTableItem(
         r.Id,
         // Date (Moved to later position in table, evenly spaced)
         Layout.Horizontal().Width(Size.Fraction(1)).Add(r.RevenueDate.ToShortDateString()),
         // Name (First column, wider)
         Layout.Horizontal()
             .Width(Size.Fraction(3))
             .Align(Align.Left)
             .Gap(0)
             .Add(new Button(r.Name, () => selectedDetailsId.Set(r.Id))
                 .Variant(ButtonVariant.Link)
             ),
         // Type (Evenly spaced)
         Layout.Horizontal().Width(Size.Fraction(1)).Add(r.Type),
         // Source (Evenly spaced)
         Layout.Horizontal().Width(Size.Fraction(1)).Add(r.Source),

         // Amount (Right aligned, evenly spaced)
         Layout.Horizontal().Width(Size.Fraction(1)).Align(Align.Right).Add(r.Amount.ToString("C")),
         r.RevenueDate,
         r.Name,
         r.Type,
         r.Source,

         r.Amount
     )).ToArray();

      allEntries.Set(tableData);
      return null;
    }

    UseEffect(LoadData, [EffectTrigger.AfterInit(), refreshToken]);

    // Handle "Sheet" Views


    // State for Create Sheet
    var showCreateSheet = UseState(false);
    if (showCreateSheet.Value)
    {
      return new RevenueCreateSheet(() => showCreateSheet.Set(false));
    }

    // Apply Filters and Search
    var filteredEntries = allEntries.Value;

    if (!string.IsNullOrWhiteSpace(searchQuery.Value))
    {
      var q = searchQuery.Value.ToLowerInvariant();
      filteredEntries = filteredEntries.Where(e =>
          e.Name.ToLowerInvariant().Contains(q) ||

          e.Type.ToLowerInvariant().Contains(q)
      ).ToArray();
    }

    if (selectedSource.Value != "All")
    {
      filteredEntries = filteredEntries.Where(e => e.Type == selectedSource.Value).ToArray();
    }

    // Apply Sorting (handled by DataTableView mostly, but initial sort useful)
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

      _ => filteredEntries
    };

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
       .Height(Size.Fit()) // Ensure it calculates height based on content
       .Gap(10)
       .Add(Layout.Horizontal()
            .Width(Size.Full())
            .Height(Size.Fit())
            .Align(Align.Center)
            .Add("Revenue Streams")
            .Add(new Spacer().Width(Size.Fraction(1))) // Force spacer to take remaining width
            .Add(new Button("Create Entry", () => showCreateSheet.Set(true))
               .Icon(Icons.Plus)
               .Variant(ButtonVariant.Primary)
            )
       )
       .Add(Layout.Horizontal()
           .Width(Size.Full())
           .Height(Size.Fit()) // Ensure input row has height
           .Gap(10)
           .Add(searchBar)
           .Add(filterSelect));

    // Projection for ToTable
    var tableData = filteredEntries.Select(r => new
    {
      IdButton = new Button($"E{r.Id:D3}", () => selectedDetailsId.Set(r.Id))
            .Variant(ButtonVariant.Ghost),
      Date = r.RevenueDate.ToShortDateString(),
      Name = r.Name,
      Type = r.Type,
      Source = r.Source,
      Amount = r.Amount.ToString("C")
    }).ToArray();

    // Use ToTable() pattern
    var table = tableData.ToTable()
         .Width(Size.Full())
         .Add(x => x.IdButton)
         .Add(x => x.Date)
         .Add(x => x.Name)
         .Add(x => x.Type)
         .Add(x => x.Source)
         .Add(x => x.Amount)
         .Header(x => x.IdButton, "ID")
         .Header(x => x.Date, "Date")
         .Header(x => x.Name, "Name")
         .Header(x => x.Type, "Type")
         .Header(x => x.Source, "Source")
         .Header(x => x.Amount, "Amount");
    // Note: Column width might not be supported via .Width(x=>...) in this API, 
    // but checking if ToTable returns Table widget which usually auto-sizes.

    return new Fragment(
        Layout.Vertical()
            .Height(Size.Full())
            .Gap(10)
            .Padding(20)
            .Add(headerContent)
            // Container for table to enforce bottom spacing
            .Add(Layout.Vertical()
                .Height(Size.Fraction(1))
                .Padding(0, 0, 0, 50)
                .Add(table)
            ),

        selectedDetailsId.Value != null ? new Dialog(
            _ => selectedDetailsId.Set((int?)null),
            new DialogHeader("Edit Entry"),
            new DialogBody(
                new RevenueEditSheet(selectedDetailsId.Value.Value, () => selectedDetailsId.Set((int?)null))
            ),
            new DialogFooter()
        ) : null
    );
  }
}
