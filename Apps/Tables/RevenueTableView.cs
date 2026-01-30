using Ivy.Shared;
using Ivy.Hooks;
using ArtistInsightTool.Apps.Views;
using ArtistInsightTool.Apps.Services;
using System.Globalization;

namespace ArtistInsightTool.Apps.Tables;

public class RevenueTableView : ViewBase
{
  private record RevenueTableItem(int Id, object DateDisplay, object PeriodDisplay, object NameDisplay, object TypeDisplay, object SourceDisplay, object AmountDisplay, DateTime RevenueDate, string Period, string Name, string Type, string Source, decimal Amount);

  public override object? Build()
  {
    var service = UseService<ArtistInsightService>();
    // State for Details (Must be declared before query to be used in lambda)
    var selectedDetailsId = UseState<int?>(() => null);
    var isEditingDetails = UseState(false);

    // UseQuery for Revenue Entries
    var revenueQuery = UseQuery("revenue_entries", async (ct) =>
    {
      var rawData = await service.GetRevenueEntriesAsync();

      // Group by Period and Source to show "Batched" view
      var grouped = rawData
        .GroupBy(e => new { e.Year, e.Quarter, e.Source })
        .Select(g =>
        {
          var first = g.First();
          var totalAmount = g.Sum(x => x.Amount);
          var fileList = string.Join(", ", g.Select(x => x.Description).Where(d => !string.IsNullOrEmpty(d)).Distinct());

          return new
          {
            Id = first.Id,
            RevenueDate = first.RevenueDate,
            Name = string.IsNullOrEmpty(fileList) ? (first.Description ?? "-") : fileList,
            Type = first.Source ?? "Unknown",
            Amount = totalAmount,
            Source = first.Integration ?? "Manual",
            Period = (first.Year.HasValue && !string.IsNullOrEmpty(first.Quarter)) ? $"{first.Year} {first.Quarter}" : "-"
          };
        })
        .OrderByDescending(e => e.RevenueDate)
        .Take(1000)
        .ToList();

      return grouped.Select(r => new RevenueTableItem(
         r.Id,
         Layout.Horizontal().Width(Size.Fraction(1)).Add(r.RevenueDate.ToShortDateString()),
         Layout.Horizontal().Width(Size.Fraction(1)).Add(r.Period),
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
         r.Period,
         r.Name,
         r.Type,
         r.Source,
         r.Amount
      )).ToArray();
    });

    var allEntries = revenueQuery.Value ?? [];
    var queryService = UseService<IQueryService>();
    var refetch = () =>
    {
      revenueQuery.Mutator.Revalidate();
      queryService.RevalidateByTag("uploads_list");
      queryService.RevalidateByTag("assets");
      queryService.RevalidateByTag("dashboard_total_revenue");
      queryService.RevalidateByTag("templates_list");
    };

    var searchQuery = UseState("");
    var sortField = UseState("Id");
    var sortDirection = UseState("Asc");
    // Filter states
    var selectedSource = UseState("All");

    // UseEffect replaced by UseQuery

    // Handle "Sheet" Views


    // State for Create Sheet
    // State for Create Sheet (Previously here, now removed)


    // Apply Filters and Search
    var filteredEntries = allEntries;

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
        new("Merchandise", "Merchandise"),
        new("Royalties", "Royalties"),
        new("Concerts", "Concerts"),
        new("Other", "Other")
    });

    var headerCard = new Card(
        Layout.Vertical().Gap(10)
            .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                 .Add(new Spacer().Width(Size.Fraction(1)))
            )
            .Add(Layout.Horizontal().Width(Size.Full()).Gap(10)
                 .Add(searchBar)
                 .Add(filterSelect)
            )
    );

    // Projection for ToTable
    var tableData = filteredEntries.Select(r => new
    {
      IdButton = new Button($"E{r.Id:D3}", () =>
      {
        selectedDetailsId.Set(r.Id);
        isEditingDetails.Set(false);
      }).Variant(ButtonVariant.Ghost),
      Date = r.RevenueDate.ToShortDateString(),
      Period = r.Period,
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
         .Add(x => x.Period)
         .Add(x => x.Name)
         .Add(x => x.Type)
         .Add(x => x.Source)
         .Add(x => x.Amount)
         .Header(x => x.IdButton, "ID")
         .Header(x => x.Date, "Date")
         .Header(x => x.Period, "Period")
         .Header(x => x.Name, "Name")
         .Header(x => x.Type, "Type")
         .Header(x => x.Source, "Source")
         .Header(x => x.Amount, "Amount");
    // Note: Column width might not be supported via .Width(x=>...) in this API, 
    // but checking if ToTable returns Table widget which usually auto-sizes.


    var viewMode = UseState("Table");

    var content = Layout.Vertical().Height(Size.Full()).Padding(20, 0, 20, 50)
        .Add(filteredEntries.Length > 0
            ? table
            : Layout.Center().Add(Text.Label("There is no information to display")));



    var visualContent = Layout.Center().Height(Size.Full())
        .Add(Text.H3("Visual View").Muted())
        .Add(Text.Label("Revenue charts coming soon").Muted());

    var mainView = viewMode.Value == "Table"
        ? (object)new HeaderLayout(headerCard, content)
        : new HeaderLayout(headerCard, visualContent);

    var floatingPanel = new FloatingPanel(
        new Button(viewMode.Value == "Table" ? "Visual View" : "Table View", () =>
        {
          viewMode.Set(viewMode.Value == "Table" ? "Visual" : "Table");
        }).Variant(ButtonVariant.Ghost).Icon(viewMode.Value == "Table" ? Icons.ChartPie : Icons.Table),
        Align.BottomCenter
    ).Offset(new Thickness(0, 0, 0, 10));

    return new Fragment(
        mainView,
        floatingPanel,

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
