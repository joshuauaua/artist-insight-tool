using Ivy.Shared;
using ArtistInsightTool.Apps.Views;
using ArtistInsightTool.Apps.Services;
using System.Globalization;

namespace ArtistInsightTool.Apps.Tables;

public class RevenueTableView : ViewBase
{
  private record RevenueTableItem(int Id, object DateDisplay, object NameDisplay, object TypeDisplay, object SourceDisplay, object AmountDisplay, DateTime RevenueDate, string Name, string Type, string Source, decimal Amount);

  public override object? Build()
  {
    var service = UseService<ArtistInsightService>();
    // var blades = UseService<IBladeController>(); // Removed Blade Dependency
    var refreshToken = this.UseRefreshToken();
    var allEntries = UseState<RevenueTableItem[]>([]);

    var searchQuery = UseState("");
    var sortField = UseState("Id");
    var sortDirection = UseState("Asc");
    // Filter states
    var selectedSource = UseState("All");

    // State for Details
    var selectedDetailsId = UseState<int?>(() => null);
    var isEditingDetails = UseState(false);

    async Task<IDisposable?> LoadData()
    {
      var rawData = await service.GetRevenueEntriesAsync();

      var tableData = rawData.Select(e => new
      {
        e.Id,
        e.RevenueDate,
        Name = e.Description ?? "-",
        Type = e.Source ?? "Unknown",

        e.Amount,
        Source = e.Integration ?? "Manual"
      })
         .OrderBy(e => e.Id)
         .Take(1000)
         .Select(r => new RevenueTableItem(
         r.Id,
         Layout.Horizontal().Width(Size.Fraction(1)).Add(r.RevenueDate.ToShortDateString()),
         Layout.Horizontal()
             .Width(Size.Fraction(3))
             .Align(Align.Left)
             .Gap(0)
             .Add(new Button(r.Name, () => selectedDetailsId.Set(r.Id))
                 .Variant(ButtonVariant.Link)
             ),
         Layout.Horizontal().Width(Size.Fraction(1)).Add(r.Type),
         Layout.Horizontal().Width(Size.Fraction(1)).Add(r.Source),

         Layout.Horizontal().Width(Size.Fraction(1)).Align(Align.Right).Add(r.Amount.ToString("C", CultureInfo.GetCultureInfo("sv-SE"))),
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
      ("Id", "Asc") => filteredEntries.OrderBy(e => e.Id).ToArray(),
      ("Id", "Desc") => filteredEntries.OrderByDescending(e => e.Id).ToArray(),
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
       .Padding(20, 20, 20, 5)
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
      IdButton = new Button($"E{r.Id:D3}", () =>
      {
        selectedDetailsId.Set(r.Id);
        isEditingDetails.Set(false);
      }).Variant(ButtonVariant.Ghost),
      Date = r.RevenueDate.ToShortDateString(),
      Name = r.Name,
      Type = r.Type,
      Source = r.Source,
      Amount = r.Amount.ToString("C", CultureInfo.GetCultureInfo("sv-SE"))
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
            .Gap(0)
            .Add(headerContent)
            // Container for table to enforce bottom spacing
            .Add(Layout.Vertical()
                .Height(Size.Fraction(1))
                .Padding(20, 0, 20, 50)
                .Add(table)
            ),

        selectedDetailsId.Value != null ? new Dialog(
            _ => selectedDetailsId.Set((int?)null),
            new DialogHeader(isEditingDetails.Value ? "Edit Entry" : "Entry Details"),
            new DialogBody(
                isEditingDetails.Value
                ? new RevenueEditSheet(selectedDetailsId.Value.Value, () =>
                  {
                    // If canceling/saving from edit, go back to view? Or close?
                    // Let's assume on Edit completion/cancel we go back to View mode first?
                    // Or simple close for now as per usual flow if saved.
                    // If we want cancel to go back to view, RevenueEditSheet needs an OnCancel.
                    // For now, let's close on Save, but maybe we can just toggle back to view on Close?
                    // Simpler: Just close everything on finish for now.
                    selectedDetailsId.Set((int?)null);
                  })
                : new RevenueViewSheet(
                    selectedDetailsId.Value.Value,
                    () => selectedDetailsId.Set((int?)null),
                    () => isEditingDetails.Set(true)
                  )
            ),
            new DialogFooter()
        ) : null
    );
  }
}
