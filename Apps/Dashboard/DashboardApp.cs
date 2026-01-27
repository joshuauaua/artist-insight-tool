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

namespace ArtistInsightTool.Apps.Dashboard;

[App(icon: Icons.LayoutDashboard, title: "Artist Ledger", path: ["Main"])]
public class DashboardApp : ViewBase
{
  // Concrete records for Hot Reload stability
  public record CategoryRevenue(string Category, double Amount);
  public record AssetRevenueItem(string Asset, double Amount);
  public record DataTableItem(int RealId, string Id, string Name, string Template, string Date);
  public record TemplateTableItem(int RealId, string Id, string Name, string Category, int Files, DateTime CreatedAt);

  // Table projection records
  public record RevenueRow(object Id, string Date, string Name, string Type, string Source, string Amount);
  public record AssetRow(object Id, string Name, string? Category, string? Type, string Amount, object Actions);
  public record DataTableRow(object Id, string Name, string Date, object Actions);
  public record TemplateRow(string Id, string Name, string Category, int Linked, object Actions);

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

    var assetsQuery = UseQuery("dashboard_assets", async (ct) => await service.GetAssetsAsync());
    var revenueQuery = UseQuery("dashboard_revenue_entries", async (ct) => await service.GetRevenueEntriesAsync());
    var totalRevenueQuery = UseQuery("dashboard_total_revenue", async (ct) => await service.GetTotalRevenueAsync(DateTime.Now.AddYears(-10), DateTime.Now));
    var tmplQuery = UseQuery("dashboard_templates", async (ct) =>
    {
      var tmpls = await service.GetTemplatesAsync();
      return tmpls.Select(t => new TemplateTableItem(t.Id, $"T{t.Id:D3}", t.Name, t.Category ?? "Other", t.RevenueEntries?.Count ?? 0, t.CreatedAt)).ToList();
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

    if (string.IsNullOrEmpty(selectedCategory.Value) && categories.Count > 0)
    {
      var defaultCat = categories.FirstOrDefault(c => string.Equals(c, "Royalties", StringComparison.OrdinalIgnoreCase)) ?? categories[0];
      selectedCategory.Set(defaultCat);
    }

    // --- 2. Early Return (Hooks ABOVE this) ---
    if (showImportSheet.Value) return new ExcelDataReaderSheet(() => showImportSheet.Set(false));

    // --- 3. View Components ---

    var tabNames = new[] { "Overview", "Analytics", "Revenue", "Assets", "Data Tables", "Templates" };
    var tabButtons = new List<object>();
    for (int i = 0; i < tabNames.Length; i++)
    {
      int index = i;
      tabButtons.Add(new Button(tabNames[i], _ => selectedTab.Set(index))
          .Variant(selectedTab.Value == index ? ButtonVariant.Primary : ButtonVariant.Ghost));
    }

    var headerCard = new Card(
        Layout.Vertical().Gap(8)
            .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                 .Add(Text.H2("Artist Ledger"))
                 .Add(new Spacer().Width(Size.Fraction(1)))
                 .Add(new Button("Import Data", () => showImportSheet.Set(true))
                     .Icon(Icons.FileUp)
                     .Variant(ButtonVariant.Primary))
            )
            .Add(Layout.Vertical().Gap(6)
                .Add(globalSearch.ToTextInput().Placeholder("Global search...").Width(Size.Full()))
                .Add(Layout.Horizontal().Gap(2).Add(tabButtons))
            )
    );

    var body = Layout.Vertical().Height(Size.Full()).Padding(20);
    if (assetsQuery.Loading || revenueQuery.Loading || totalRevenueQuery.Loading || tmplQuery.Loading)
    {
      body.Add(Layout.Center().Add(Text.Label("Syncing Ledger...")));
    }
    else
    {
      body.Add(selectedTab.Value switch
      {
        0 => RenderOverview(assets, totalRevenue, revenueEntries),
        1 => RenderAnalyticsTab(assets, selectedCategory, categories),
        2 => RenderRevenue(revenueEntries, globalSearch, selectedDialog, revenueQuery),
        3 => RenderAssets(assets, globalSearch, selectedDialog, assetsQuery, service),
        4 => RenderDataTables(revenueEntries, globalSearch, selectedDialog, revenueQuery, service),
        5 => RenderTemplates(templatesData, globalSearch, selectedDialog, tmplQuery, service, client),
        _ => RenderOverview(assets, totalRevenue, revenueEntries)
      });
    }

    return new Fragment(new HeaderLayout(headerCard, body), selectedDialog.Value);
  }

  // --- Render Methods (Private class methods to ensure metadata stability) ---

  private object RenderOverview(List<Asset> assets, MetricDto? totalRevenue, List<RevenueEntryDto> revenueEntries) => Layout.Vertical().Gap(20)
      .Add(Layout.Grid().Columns(3).Gap(20)
          .Add(new Card(Layout.Center().Add(Text.H2(assets.Count.ToString()))).Title("Total Assets"))
          .Add(new Card(Layout.Center().Add(Text.H2(totalRevenue?.Value ?? "$0.00"))).Title("Total Revenue"))
          .Add(new Card(Layout.Center().Add(Text.H2(revenueEntries.Count(x => !string.IsNullOrEmpty(x.JsonData)).ToString()))).Title("Data Imports")))
      .Add(new Card(Text.P("Welcome to your Artist Ledger. Use the tabs above to manage your assets and revenue.")).Title("Quick Start"));
  private object RenderAnalyticsTab(List<Asset> assets, IState<string> selectedCategory, List<string> categories)
  {
    if (assets.Count == 0 || !categories.Any()) return Layout.Center().Add(Text.Label("No asset data available."));

    var currentCat = selectedCategory.Value;
    if (string.IsNullOrEmpty(currentCat)) currentCat = categories.FirstOrDefault() ?? "Uncategorized";

    var categoryData = assets
        .GroupBy(a => (a.Category ?? "Uncategorized").Trim())
        .Select(g => new CategoryRevenue(g.Key, (double)g.Sum(a => a.AmountGenerated)))
        .ToList();

    var assetBreakdown = assets
        .Where(a => string.Equals((a.Category ?? "Uncategorized").Trim(), currentCat.Trim(), StringComparison.OrdinalIgnoreCase))
        .Select(a => new AssetRevenueItem(a.Name, (double)a.AmountGenerated))
        .OrderByDescending(a => a.Amount)
        .ToList();

    return Layout.Vertical().Gap(20)
        .Add(Layout.Horizontal().Gap(20).Width(Size.Full())
            .Add(new Card(
                new PieChart(categoryData)
                    .Pie("Amount", "Category")
                    .Tooltip()
            ).Title("Revenue by Category").Width(Size.Fraction(0.5f)))
            .Add(new Card(
                new PieChart(assetBreakdown)
                    .Pie("Amount", "Asset")
                    .Tooltip()
            ).Title($"{currentCat} Breakdown").Width(Size.Fraction(0.5f))))
        .Add(Layout.Horizontal().Align(Align.Center).Gap(10)
            .Add(Text.Label("Drilldown Category:"))
            .Add(selectedCategory.ToSelectInput(categories.ToOptions()).Width(300)));
  }


  private object RenderRevenue(List<RevenueEntryDto> entries, IState<string> search, IState<object?> dialog, dynamic query)
  {
    var filtered = entries.Where(e => string.IsNullOrWhiteSpace(search.Value) || (e.Description ?? "").Contains(search.Value, StringComparison.OrdinalIgnoreCase)).ToList();
    return Layout.Vertical().Gap(20)
        .Add(Layout.Horizontal().Align(Align.Center).Add(Text.H4($"{filtered.Count} Streams")).Add(new Spacer().Width(Size.Fraction(1))).Add(new Button("Create Entry", () => dialog.Set(new RevenueCreateSheet(() => { dialog.Set((object?)null); query.Mutator.Revalidate(); }))).Icon(Icons.Plus).Variant(ButtonVariant.Outline)))
        .Add(filtered.Select(r => new RevenueRow(
            new Button($"E{r.Id:D3}", () => dialog.Set(new RevenueViewSheet(r.Id, () => dialog.Set((object?)null), () => dialog.Set(new RevenueEditSheet(r.Id, () => dialog.Set((object?)null)))))).Variant(ButtonVariant.Ghost),
            r.RevenueDate.ToShortDateString(),
            r.Description ?? "-",
            r.Source ?? "Unknown", r.Integration ?? "Manual", r.Amount.ToString("C", CultureInfo.GetCultureInfo("sv-SE"))
        )).Take(100).ToArray().ToTable().Width(Size.Full()).Add(x => x.Id).Add(x => x.Date).Add(x => x.Name).Add(x => x.Type).Add(x => x.Source).Add(x => x.Amount));
  }

  private object RenderAssets(List<Asset> assets, IState<string> search, IState<object?> dialog, dynamic query, ArtistInsightService service)
  {
    var filtered = assets.Where(a => string.IsNullOrWhiteSpace(search.Value) || a.Name.Contains(search.Value, StringComparison.OrdinalIgnoreCase)).ToList();
    return Layout.Vertical().Gap(20)
        .Add(Layout.Horizontal().Align(Align.Center).Add(Text.H4($"{filtered.Count} Assets")).Add(new Spacer().Width(Size.Fraction(1))).Add(new Button("Create Asset", () => dialog.Set(new CreateAssetSheet(() => { dialog.Set((object?)null); query.Mutator.Revalidate(); }))).Icon(Icons.Plus).Variant(ButtonVariant.Outline)))
        .Add(filtered.Select(a => new AssetRow(
            new Button($"A{a.Id:D3}", () => dialog.Set(new AssetViewSheet(a.Id, () => { dialog.Set((object?)null); query.Mutator.Revalidate(); }))).Variant(ButtonVariant.Ghost),
            a.Name, a.Category, a.Type, a.AmountGenerated.ToString("C", CultureInfo.GetCultureInfo("sv-SE")),
            new Button("", () => dialog.Set(new Dialog(_ => { dialog.Set((object?)null); return ValueTask.CompletedTask; }, new DialogHeader("Delete Asset"), new DialogBody(Text.Label($"Delete {a.Name}?")), new DialogFooter(new Button("Cancel", () => dialog.Set((object?)null)), new Button("Delete", async () => { await service.DeleteAssetAsync(a.Id); dialog.Set((object?)null); query.Mutator.Revalidate(); }).Variant(ButtonVariant.Destructive))))).Icon(Icons.Trash).Variant(ButtonVariant.Ghost)
        )).Take(100).ToArray().ToTable().Width(Size.Full()).Add(x => x.Id).Add(x => x.Name).Add(x => x.Category).Add(x => x.Type).Add(x => x.Amount).Add(x => x.Actions).Header(x => x.Id, "ID").Header(x => x.Actions, ""));
  }

  private object RenderDataTables(List<RevenueEntryDto> entries, IState<string> search, IState<object?> dialog, dynamic query, ArtistInsightService service)
  {
    var items = new List<DataTableItem>();
    foreach (var e in entries.Where(x => !string.IsNullOrEmpty(x.JsonData)))
    {
      try
      {
        using var doc = JsonDocument.Parse(e.JsonData!);
        var fn = doc.RootElement[0].TryGetProperty("FileName", out var p) ? p.GetString() : null;
        items.Add(new DataTableItem(e.Id, $"DT{e.Id:D3}", fn ?? e.Description ?? "Table", "-", e.RevenueDate.ToShortDateString()));
      }
      catch { }
    }
    var filtered = items.Where(t => string.IsNullOrWhiteSpace(search.Value) || t.Name.Contains(search.Value, StringComparison.OrdinalIgnoreCase)).ToList();
    return Layout.Vertical().Gap(20).Add(Text.H4($"{filtered.Count} Tables"))
        .Add(filtered.Select(t => new DataTableRow(
            new Button(t.Id, () => dialog.Set(new DataTableViewSheet(t.RealId, () => dialog.Set((object?)null)))).Variant(ButtonVariant.Ghost),
            t.Name, t.Date,
            new Button("", () => dialog.Set(new Dialog(_ => { dialog.Set((object?)null); return ValueTask.CompletedTask; }, new DialogHeader("Delete Data"), new DialogBody(Text.Label($"Delete {t.Name}?")), new DialogFooter(new Button("Cancel", () => dialog.Set((object?)null)), new Button("Delete", async () => { await service.DeleteRevenueEntryAsync(t.RealId); dialog.Set((object?)null); query.Mutator.Revalidate(); }).Variant(ButtonVariant.Destructive))))).Icon(Icons.Trash).Variant(ButtonVariant.Ghost)
        )).Take(100).ToArray().ToTable().Width(Size.Full()).Add(x => x.Id).Add(x => x.Name).Add(x => x.Date).Add(x => x.Actions).Header(x => x.Id, "ID").Header(x => x.Actions, ""));
  }

  private object RenderTemplates(List<TemplateTableItem> templates, IState<string> search, IState<object?> dialog, dynamic query, ArtistInsightService service, IClientProvider client)
  {
    var filtered = templates.Where(t => string.IsNullOrWhiteSpace(search.Value) || t.Name.Contains(search.Value, StringComparison.OrdinalIgnoreCase)).ToList();
    return Layout.Vertical().Gap(20)
        .Add(Layout.Horizontal().Align(Align.Center).Add(Text.H4($"{filtered.Count} Templates")).Add(new Spacer().Width(Size.Fraction(1))).Add(new Button("New Template", () => dialog.Set(new CreateTemplateSheet(() => { dialog.Set((object?)null); query.Mutator.Revalidate(); }))).Icon(Icons.Plus).Variant(ButtonVariant.Outline)))
        .Add(filtered.Select(t => new TemplateRow(
            $"T{t.RealId:D3}", t.Name, t.Category, t.Files,
            Layout.Horizontal().Gap(5)
                .Add(new Button("", () => dialog.Set(new TemplateEditSheet(() => { dialog.Set((object?)null); query.Mutator.Revalidate(); }, t.RealId, client))).Icon(Icons.Pencil).Variant(ButtonVariant.Ghost))
                .Add(new Button("", () => dialog.Set(new Dialog(_ => { dialog.Set((object?)null); return ValueTask.CompletedTask; }, new DialogHeader("Delete Template"), new DialogBody(Text.Label($"Delete {t.Name}?")), new DialogFooter(new Button("Cancel", () => dialog.Set((object?)null)), new Button("Delete", async () => { await service.DeleteTemplateAsync(t.RealId); dialog.Set((object?)null); query.Mutator.Revalidate(); }).Variant(ButtonVariant.Destructive))))).Icon(Icons.Trash).Variant(ButtonVariant.Ghost))
        )).Take(100).ToArray().ToTable().Width(Size.Full()).Add(x => x.Id).Add(x => x.Name).Add(x => x.Category).Add(x => x.Linked).Add(x => x.Actions).Header(x => x.Id, "ID").Header(x => x.Linked, "Files").Header(x => x.Actions, ""));
  }
}
