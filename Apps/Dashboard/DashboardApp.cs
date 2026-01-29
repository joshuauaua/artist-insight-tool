using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using ArtistInsightTool.Apps.Services;
using ArtistInsightTool.Apps.Views;
using ArtistInsightTool.Apps.Tables;
using ExcelDataReaderExample;
using Ivy.Hooks;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;
using Ivy.Core.Hooks;
using ArtistInsightTool.Apps.Views.Kanban;

namespace ArtistInsightTool.Apps.Dashboard;

[App(icon: Icons.LayoutDashboard, title: "Artist Ledger", path: ["Main"])]
public class DashboardApp : ViewBase
{
  public record PieChartData(string Dimension, double Measure);
  public record UploadTableItem(int RealId, string Id, string Name, string Template, string UploadDate, string Period);
  public record TemplateTableItem(int RealId, string Id, string Name, string Source, string Category, int Files, DateTime CreatedAt);

  // Table projection records
  public record RevenueRow(object Id, string Date, string Name, string Type, string Source, string Amount);
  public record AssetRow(object Id, string Name, string? Category, string? Type, string Amount, object Actions);
  public record UploadRow(object Id, string Name, string Template, string Period, string UploadDate, object Actions);
  public record TemplateRow(object Id, string Name, string Source, string Category, int Linked, object Actions);

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();

    // --- 1. Hook Sequence (Order-critical) ---
    var globalSearch = UseState("");
    var selectedTab = UseState(0);
    var showImportSheet = UseState(false);
    var selectedDialog = UseState<object?>(() => null);

    // Kanban State
    var overviewCards = UseState(new List<CardState>
    {
        // Col 1
        new("quick-start", "Quick Start", " ", 0, 0),
        new("data-imports", "Data Imports", " ", 1, 3),
        new("p1", "", " ", 2, 4),

        // Col 2
        new("total-assets", "Total Assets", "  ", 0, 1),
        new("p2", "", "  ", 1, 4),
        new("p3", "", "  ", 2, 4),

        // Col 3
        new("total-revenue", "Total Revenue", "   ", 0, 2),
        new("p4", "", "   ", 1, 4),
        new("p5", "", "   ", 2, 4),
    });

    var assetsQuery = UseQuery("assets", async (ct) => await service.GetAssetsAsync());
    var revenueQuery = UseQuery("revenue_entries", async (ct) => await service.GetRevenueEntriesAsync());
    var totalRevenueQuery = UseQuery("dashboard_total_revenue", async (ct) => await service.GetTotalRevenueAsync(DateTime.Now.AddYears(-10), DateTime.Now));
    var tmplQuery = UseQuery("templates_list", async (ct) =>
    {
      var tmpls = await service.GetTemplatesAsync();
      return tmpls.Select(t => new TemplateTableItem(t.Id, $"T{t.Id:D3}", t.Name, t.SourceName ?? "-", t.Category ?? "Other", t.RevenueEntries?.Count ?? 0, t.CreatedAt)).ToList();
    });

    // Derive collections
    var assets = assetsQuery.Value ?? [];
    var revenueEntries = revenueQuery.Value ?? [];
    var totalRevenue = totalRevenueQuery.Value;
    var templatesData = tmplQuery.Value ?? [];

    var categories = assets.GroupBy(a => (a.Category ?? "Uncategorized").Trim())
        .Select(g => g.Key)
        .OrderBy(c => c == "Royalties" ? 0 : 1)
        .ThenBy(c => c)
        .ToList();

    var selectedCategory = UseState("");

    void InitCategory()
    {
      if (string.IsNullOrEmpty(selectedCategory.Value) && categories.Count > 0)
      {
        var defaultCat = categories.FirstOrDefault(c => string.Equals(c, "Royalties", StringComparison.OrdinalIgnoreCase)) ?? categories[0];
        // Schedule update to avoid render loop conflict if called directly (though Set usually handles it)
        selectedCategory.Set(defaultCat);
      }
    }

    // Trigger initialization on every render (guarded by logic inside)
    UseEffect(InitCategory);


    // --- 2. Early Return (Hooks ABOVE this) ---
    if (showImportSheet.Value) return new ExcelDataReaderSheet(() => showImportSheet.Set(false), () =>
    {
      revenueQuery.Mutator.Revalidate();
      assetsQuery.Mutator.Revalidate();
      totalRevenueQuery.Mutator.Revalidate();
      selectedTab.Set(3);
    });

    // --- 3. View Components ---

    // --- 3. View Components and Layout ---

    var body = Layout.Vertical().Height(Size.Full()).Padding(4, 4, 4, 4);
    if (assetsQuery.Loading || revenueQuery.Loading || totalRevenueQuery.Loading || tmplQuery.Loading)
    {
      body.Add(Layout.Center().Add(Text.Label("Syncing Ledger...")));
    }
    else
    {
      body.Add(selectedTab.Value switch
      {
        0 => RenderOverview(showImportSheet, overviewCards, assets, totalRevenue, revenueEntries, selectedDialog),
        1 => RenderAssets(assets, globalSearch, selectedDialog, assetsQuery, service, selectedCategory, categories, revenueEntries),
        2 => RenderRevenue(revenueEntries, globalSearch, selectedDialog, revenueQuery),
        3 => RenderUploads(revenueEntries, globalSearch, selectedDialog, revenueQuery, service),
        4 => RenderTemplates(templatesData, globalSearch, selectedDialog, tmplQuery, service, client),
        _ => RenderOverview(showImportSheet, overviewCards, assets, totalRevenue, revenueEntries, selectedDialog)
      });
    }

    var header = new DashboardHeader(selectedTab, () => showImportSheet.Set(true));
    var mainView = new HeaderLayout(header, body);

    return new Fragment(mainView, selectedDialog.Value);
  }

  // --- Render Methods (Private class methods to ensure metadata stability) ---

  // Kanban State Record
  public record CardState(string Id, string Title, string Column, int Order, int Type); // Type: 0=QuickStart, 1=Assets, 2=Revenue, 3=Imports, 4=Placeholder

  private object RenderOverview(IState<bool> showImportSheet, IState<List<CardState>> cardStates, List<Asset> assets, MetricDto? totalRevenue, List<RevenueEntryDto> revenueEntries, IState<object?> selectedDialog)
  {
    return cardStates.Value
        .ToKanban(
            groupBySelector: c => c.Column,
            idSelector: c => c.Id,
            orderSelector: c => c.Order)
        .HideCounts()
        .Gap(4)
        .CardBuilder(c =>
        {
          // Render content based on Type/Id
          var content = c.Type switch
          {
            0 => new Card(
                Layout.Vertical().Gap(5)
                    .Add(Text.P("1. Use the 'Uploads' tab to begin importing your data."))
                    .Add(Text.P("2. Manage your products in 'Assets'."))
                    .Add(Text.P("3. Customize your experience in Overview.")),
                new Button("Get Started", _ => showImportSheet.Set(true))
            ).Title("Get Started")
             .Description("Build your catalog and start tracking your revenue.")
             .BorderThickness(1)
             .BorderStyle(BorderStyle.Dashed)
             .BorderColor(Colors.Primary)
             .BorderRadius(BorderRadius.Rounded)
             .Width(Size.Fraction(1.0f)),

            1 => new Card(
                Layout.Vertical().Gap(10).Padding(10).Align(Align.Center) // Symmetrical padding
                    .Add(Text.H4(c.Title))
                    .Add(new Icon(Icons.Package).Size(14).Color(Colors.Gray))
                    .Add(Text.H2(assets.Count.ToString()))
            ),
            2 => new Card(
                Layout.Vertical().Gap(10).Padding(10).Align(Align.Center) // Symmetrical padding
                    .Add(Text.H4(c.Title))
                    .Add(new Icon(Icons.DollarSign).Size(14).Color(Colors.Gray))
                    .Add(Text.H2((totalRevenue?.Value.Replace("$", "").Replace("USD", "").Replace("SEK", "").Trim() ?? "0") + " SEK"))
            ),
            3 => new Card(
                Layout.Vertical().Gap(10).Padding(10).Align(Align.Center) // Symmetrical padding
                    .Add(Text.H4(c.Title))
                    .Add(new Icon(Icons.FileClock).Size(14).Color(Colors.Gray))
                    .Add(Text.H2(revenueEntries.Count(x => !string.IsNullOrEmpty(x.JsonData)).ToString()))
            ),
            _ => new Card(
                    Layout.Center().Size(Size.Full())
                        .Add(new Button("", () => selectedDialog.Set(RenderAddWidgetDialog(selectedDialog))).Icon(Icons.Plus).Variant(ButtonVariant.Ghost))
                )
                .BorderColor(Colors.Gray)
                .BorderStyle(BorderStyle.Dashed)
                .BorderThickness(1)
                .BorderRadius(BorderRadius.Rounded)
          };
          return content.Height(Size.Units(75));
        })
        .ColumnOrder(c => c.Column)
        .Width(Size.Full())
        .ColumnWidth(Size.Fraction(0.33f))
        .HandleMove(moveData =>
        {
          var cardId = moveData.CardId?.ToString();
          if (string.IsNullOrEmpty(cardId)) return;

          var currentList = cardStates.Value.ToList();
          var item = currentList.FirstOrDefault(x => x.Id == cardId);
          if (item == null) return;

          // Remove item
          currentList.Remove(item);

          // Find target index in destination column
          var targetColItems = currentList.Where(x => x.Column == moveData.ToColumn).OrderBy(x => x.Order).ToList();

          int insertIndex = -1;
          if (moveData.TargetIndex.HasValue)
          {
            // TargetIndex is relative to the column view
            if (moveData.TargetIndex.Value < targetColItems.Count)
            {
              var targetItem = targetColItems[moveData.TargetIndex.Value];
              insertIndex = currentList.IndexOf(targetItem);
            }
            else
            {
              // End of column
              insertIndex = currentList.Count;
            }
          }

          // Adjust item
          var updatedItem = item with { Column = moveData.ToColumn };

          if (insertIndex >= 0 && insertIndex <= currentList.Count)
            currentList.Insert(insertIndex, updatedItem);
          else
            currentList.Add(updatedItem);

          // Re-normalize orders (optional but good for stability)
          for (int i = 0; i < currentList.Count; i++) currentList[i] = currentList[i] with { Order = i };

          cardStates.Set(currentList);
        });
  }

  private object RenderAnalyticsCard(List<Asset> assets, IState<string> selectedCategory, List<string> categories)
  {
    if (assets.Count == 0 || !categories.Any()) return Layout.Center().Add(Text.Label("No asset data available."));

    var currentCat = selectedCategory.Value;
    if (string.IsNullOrEmpty(currentCat)) currentCat = categories.FirstOrDefault() ?? "Uncategorized";

    // Summary data for left chart
    var categoryData = assets
        .GroupBy(a => (a.Category ?? "Uncategorized").Trim())
        .Select(g => new PieChartData(g.Key, (double)g.Sum(a => a.AmountGenerated)))
        .Where(x => x.Measure > 0)
        .ToList();

    // Asset breakdown for selected category (right chart)
    var selectedCategoryAssets = assets
        .Where(a => string.Equals((a.Category ?? "Uncategorized").Trim(), currentCat.Trim(), StringComparison.OrdinalIgnoreCase))
        .Select(a => new PieChartData(a.Name, (double)a.AmountGenerated))
        .Where(x => x.Measure > 0)
        .OrderByDescending(x => x.Measure)
        .ToList();

    return new Card(
        Layout.Vertical().Gap(10)
            .Add(Layout.Horizontal().Align(Align.Center).Gap(10)
                .Add(Text.H4("Revenue Analytics"))
                .Add(new Spacer())
                .Add(Text.Label("Drilldown Category:"))
                .Add(selectedCategory.ToSelectInput(categories.ToOptions()).Width(150))
            )
            .Add(Layout.Horizontal().Gap(10).Width(Size.Full())
                .Add(Layout.Vertical().Width(Size.Fraction(0.5f)).Gap(5)
                    .Add(Layout.Center().Add(Text.Label("Total Revenue by Category").Small()))
                    .Add(new PieChart(categoryData)
                        .Pie("Measure", "Dimension")
                        .Tooltip()
                        .Height(Size.Units(100)))
                )
                .Add(Layout.Vertical().Width(Size.Fraction(0.5f)).Gap(5)
                    .Add(Layout.Center().Add(Text.Label($"{currentCat} - Asset Breakdown").Small()))
                    .Add(new PieChart(selectedCategoryAssets)
                        .Pie("Measure", "Dimension")
                        .Tooltip()
                        .Height(Size.Units(100)))
                )
            )
    )
    .BorderStyle(BorderStyle.Dashed)
    .BorderColor(Colors.Primary);
  }


  private object RenderRevenue(List<RevenueEntryDto> entries, IState<string> search, IState<object?> dialog, dynamic query)
  {
    var filtered = entries.Where(e => string.IsNullOrWhiteSpace(search.Value) || (e.Description ?? "").Contains(search.Value, StringComparison.OrdinalIgnoreCase)).ToList();

    // Calculate metrics
    var totalAmount = entries.Sum(e => e.Amount); // Metric "Revenue"
    var target = 1000m;
    var progress = target > 0 ? (double)(totalAmount / target) : 0;
    var percentage = progress * 100;

    return Layout.Vertical().Gap(20)
        .Add(new Card(
             Layout.Horizontal().Align(Align.Center).Gap(15)
                .Add(new Icon(Icons.DollarSign).Size(24))
                .Add(Layout.Vertical().Gap(5)
                    .Add(Text.Label("Total Revenue").Small())
                    .Add(Text.H3(totalAmount.ToString("C", CultureInfo.GetCultureInfo("sv-SE"))))
                    .Add(Layout.Horizontal().Gap(5).Align(Align.Center)
                        .Add(Text.Label($"{percentage:F0}% of Goal").Color(Colors.Green))
                        .Add(Text.Label($"Target: {target.ToString("C0", CultureInfo.GetCultureInfo("sv-SE"))}").Small())
                    )
                )
        ).Width(Size.Full()))
        .Add(filtered.Select(r => new RevenueRow(
            new Button($"E{r.Id:D3}", () => dialog.Set(new RevenueViewSheet(r.Id, () => dialog.Set((object?)null), () => dialog.Set(new RevenueEditSheet(r.Id, () => dialog.Set((object?)null)))))).Variant(ButtonVariant.Ghost),
            r.RevenueDate.ToShortDateString(),
            r.Description ?? "-",
            r.Source ?? "Unknown", r.Integration ?? "Manual", r.Amount.ToString("C", CultureInfo.GetCultureInfo("sv-SE"))
        )).Take(100).ToArray().ToTable().Width(Size.Full()).Add(x => x.Id).Add(x => x.Date).Add(x => x.Name).Add(x => x.Type).Add(x => x.Source).Add(x => x.Amount));
  }

  private object RenderAssets(List<Asset> assets, IState<string> search, IState<object?> dialog, dynamic query, ArtistInsightService service, IState<string> selectedCategory, List<string> categories, List<RevenueEntryDto> revenueEntries)
  {
    var filtered = assets.Where(a => string.IsNullOrWhiteSpace(search.Value) || a.Name.Contains(search.Value, StringComparison.OrdinalIgnoreCase)).ToList();

    // Line Chart Logic
    var chartData = revenueEntries
        .GroupBy(e => new { e.RevenueDate.Year, e.RevenueDate.Month })
        .Select(g => new
        {
          Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
          Revenue = (double)g.Sum(e => e.Amount)
        })
        .OrderBy(x => DateTime.ParseExact(x.Month, "MMM yyyy", CultureInfo.InvariantCulture))
        .ToArray();

    return Layout.Vertical().Gap(20)
        // 1. Search and Table at the top
        .Add(Layout.Vertical().Gap(10)
            .Add(Layout.Horizontal().Align(Align.Center)
                .Add(search.ToTextInput().Placeholder("Search assets...").Width(300))
                .Add(new Spacer())
            )
            .Add(filtered.Select(a => new AssetRow(
                new Button($"A{a.Id:D3}", () => dialog.Set(new AssetViewSheet(a.Id, () => { dialog.Set((object?)null); query.Mutator.Revalidate(); }))).Variant(ButtonVariant.Ghost),
                a.Name, a.Category, a.Type, a.AmountGenerated.ToString("C", CultureInfo.GetCultureInfo("sv-SE")),
                new Button("", () => dialog.Set(new Dialog(_ => { dialog.Set((object?)null); return ValueTask.CompletedTask; }, new DialogHeader("Delete Asset"), new DialogBody(Text.Label($"Delete {a.Name}?")), new DialogFooter(new Button("Cancel", () => dialog.Set((object?)null)), new Button("Delete", async () => { await service.DeleteAssetAsync(a.Id); dialog.Set((object?)null); query.Mutator.Revalidate(); }).Variant(ButtonVariant.Destructive))))).Icon(Icons.Trash).Variant(ButtonVariant.Ghost)
            )).Take(100).ToArray().ToTable().Width(Size.Full()).Add(x => x.Id).Add(x => x.Name).Add(x => x.Category).Add(x => x.Type).Add(x => x.Amount).Add(x => x.Actions).Header(x => x.Id, "ID").Header(x => x.Actions, ""))
        )
        // 2. Side-by-side widgets below
        .Add(Layout.Horizontal().Gap(20).Width(Size.Full())
            .Add(Layout.Vertical().Width(Size.Fraction(0.5f))
                .Add(RenderAnalyticsCard(assets, selectedCategory, categories))
            )
            .Add(Layout.Vertical().Width(Size.Fraction(0.5f))
                .Add(new Card(
                     Layout.Vertical().Gap(10)
                        .Add(Text.H4("Revenue History"))
                        .Add(Layout.Vertical().Height(Size.Units(200)) // Adjusted height for side-by-side
                            .Add(chartData.ToLineChart(style: LineChartStyles.Dashboard)
                                .Dimension("Month", e => e.Month)
                                .Measure("Revenue", e => e.Sum(f => f.Revenue))
                                .Toolbox()
                            )
                        )
                ).Width(Size.Full()).BorderStyle(BorderStyle.Dashed).BorderColor(Colors.Primary))
            )
        );
  }

  private object RenderUploads(List<RevenueEntryDto> entries, IState<string> search, IState<object?> dialog, dynamic query, ArtistInsightService service)
  {
    var items = new List<UploadTableItem>();
    foreach (var e in entries.Where(x => !string.IsNullOrEmpty(x.JsonData)))
    {
      try
      {
        using var doc = JsonDocument.Parse(e.JsonData!);
        var fn = doc.RootElement[0].TryGetProperty("FileName", out var p) ? p.GetString() : null;
        var period = (e.Year.HasValue && !string.IsNullOrEmpty(e.Quarter)) ? $"{e.Year} {e.Quarter}" : "-";
        var uploadDate = (e.UploadDate ?? e.RevenueDate).ToShortDateString();
        items.Add(new UploadTableItem(e.Id, $"DT{e.Id:D3}", fn ?? e.Description ?? "Table", e.ImportTemplateName ?? "-", uploadDate, period));
      }
      catch { }
    }
    var filtered = items.Where(t => string.IsNullOrWhiteSpace(search.Value) || t.Name.Contains(search.Value, StringComparison.OrdinalIgnoreCase)).ToList();
    return Layout.Vertical().Gap(20)
        .Add(filtered.Select(t => new UploadRow(
            new Button(t.Id, () => dialog.Set(new UploadViewSheet(t.RealId, () => dialog.Set((object?)null)))).Variant(ButtonVariant.Ghost),
            t.Name,
            t.Template,
            t.Period,
            t.UploadDate,
            new Button("", () => dialog.Set(new Dialog(_ => { dialog.Set((object?)null); return ValueTask.CompletedTask; }, new DialogHeader("Delete Data"), new DialogBody(Text.Label($"Delete {t.Name}?")), new DialogFooter(new Button("Cancel", () => dialog.Set((object?)null)), new Button("Delete", async () => { await service.DeleteRevenueEntryAsync(t.RealId); dialog.Set((object?)null); query.Mutator.Revalidate(); }).Variant(ButtonVariant.Destructive))))).Icon(Icons.Trash).Variant(ButtonVariant.Ghost)
        )).Take(100).ToArray().ToTable().Width(Size.Full()).Add(x => x.Id).Add(x => x.Name).Add(x => x.Template).Add(x => x.Period).Add(x => x.UploadDate).Add(x => x.Actions).Header(x => x.Id, "ID").Header(x => x.Template, "Template").Header(x => x.Period, "Period").Header(x => x.UploadDate, "Upload Date").Header(x => x.Actions, ""));
  }

  private object RenderTemplates(List<TemplateTableItem> templates, IState<string> search, IState<object?> dialog, dynamic query, ArtistInsightService service, IClientProvider client)
  {
    var filtered = templates.Where(t => string.IsNullOrWhiteSpace(search.Value) || t.Name.Contains(search.Value, StringComparison.OrdinalIgnoreCase)).ToList();
    return Layout.Vertical().Gap(20)
        .Add(filtered.Select(t => new TemplateRow(
            new Button($"T{t.RealId:D3}", () => dialog.Set(new TemplateDataViewSheet(t.RealId, () => dialog.Set((object?)null)))).Variant(ButtonVariant.Ghost),
            t.Name, t.Source, t.Category, t.Files,
            Layout.Horizontal().Gap(5)
                .Add(new Button("", () => dialog.Set(new TemplateEditSheet(() => { dialog.Set((object?)null); query.Mutator.Revalidate(); }, t.RealId, client))).Icon(Icons.Pencil).Variant(ButtonVariant.Ghost))
                .Add(new Button("", () => dialog.Set(new Dialog(_ => { dialog.Set((object?)null); return ValueTask.CompletedTask; }, new DialogHeader("Delete Template"), new DialogBody(Text.Label($"Delete {t.Name}?")), new DialogFooter(new Button("Cancel", () => dialog.Set((object?)null)), new Button("Delete", async () => { await service.DeleteTemplateAsync(t.RealId); dialog.Set((object?)null); query.Mutator.Revalidate(); }).Variant(ButtonVariant.Destructive))))).Icon(Icons.Trash).Variant(ButtonVariant.Ghost))
        )).Take(100).ToArray().ToTable().Width(Size.Full())
            .Add(x => x.Id).Add(x => x.Name).Add(x => x.Source).Add(x => x.Category).Add(x => x.Linked).Add(x => x.Actions)
            .Header(x => x.Id, "ID").Header(x => x.Source, "Source").Header(x => x.Linked, "Files").Header(x => x.Actions, ""));
  }


  //Dialog/Modal for adding a new insight card
  private object RenderAddWidgetDialog(IState<object?> selectedDialog)
  {
    return new Dialog(
        _ => { selectedDialog.Set((object?)null); return ValueTask.CompletedTask; },
        new DialogHeader("Add an Insight Card"),
        new DialogBody(Layout.Vertical().Gap(10).Add(Text.P("Select an insight to add to your dashboard."))),
        new DialogFooter(new Button("Close", () => selectedDialog.Set((object?)null)).Variant(ButtonVariant.Ghost))
    );
  }
}
