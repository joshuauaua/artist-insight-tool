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

namespace ArtistInsightTool.Apps.Dashboard;

[App(icon: Icons.LayoutDashboard, title: "Artist Ledger", path: ["Main"])]
public class DashboardApp : ViewBase
{
  // Concrete records for Hot Reload stability
  public record CategoryRevenue(string Category, double Amount);
  public record AssetRevenueItem(string Asset, double Amount);
  public record DataTableItem(int RealId, string Id, string Name, string Template, string Date);
  public record TemplateTableItem(int RealId, string Id, string Name, string Category, int Files, DateTime CreatedAt);

  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();

    // Global Search and Tab State
    var globalSearch = UseState("");
    var selectedTab = UseState(0);
    var tabs = new[] { "Overview", "Analytics", "Revenue", "Assets", "Data Tables", "Templates" };

    // Global Overlay State
    var showImportSheet = UseState(false);
    var selectedDialog = UseState<object?>(() => null);

    // --- Top-Level Hooks (Must be outside condition logic) ---

    // 1. Assets
    var assetsQuery = UseQuery("dashboard_assets", async (ct) => await service.GetAssetsAsync());
    var assets = assetsQuery.Value ?? [];

    // 2. Revenue Entries
    var revenueQuery = UseQuery("dashboard_revenue_entries", async (ct) => await service.GetRevenueEntriesAsync());
    var revenueEntries = revenueQuery.Value ?? [];

    // 3. Total Revenue (for Overview)
    var totalRevenueQuery = UseQuery("dashboard_total_revenue", async (ct) => await service.GetTotalRevenueAsync(DateTime.Now.AddYears(-10), DateTime.Now));
    var totalRevenue = totalRevenueQuery.Value;

    // 4. Templates
    var tmplQuery = UseQuery("dashboard_templates", async (ct) =>
    {
      var tmpls = await service.GetTemplatesAsync();
      return tmpls.Select(t => new TemplateTableItem(t.Id, $"T{t.Id:D3}", t.Name, t.Category ?? "Other", t.RevenueEntries?.Count ?? 0, t.CreatedAt)).ToList();
    });
    var templatesData = tmplQuery.Value ?? [];

    // 5. Data Tables (Derived from revenueEntries)
    var dataTableItems = new List<DataTableItem>();
    foreach (var e in revenueEntries.Where(x => !string.IsNullOrEmpty(x.JsonData)))
    {
      try
      {
        using var doc = JsonDocument.Parse(e.JsonData!);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
          var first = root[0];
          if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("FileName", out var fn))
            dataTableItems.Add(new DataTableItem(e.Id, $"DT{e.Id:D3}", fn.GetString() ?? "Table", "-", e.RevenueDate.ToShortDateString()));
          else dataTableItems.Add(new DataTableItem(e.Id, $"DT{e.Id:D3}", e.Description ?? "Legacy", "-", e.RevenueDate.ToShortDateString()));
        }
      }
      catch { }
    }

    // Analytics State (Drilldown)
    var categories = assets.GroupBy(a => a.Category ?? "Uncategorized").Select(g => g.Key).OrderBy(c => c).ToList();
    var selectedCategory = UseState(categories.FirstOrDefault() ?? "Uncategorized");

    // Import Handling
    if (showImportSheet.Value)
    {
      return new ExcelDataReaderSheet(() =>
      {
        showImportSheet.Set(false);
      });
    }

    var headerCard = new Card(
        Layout.Vertical().Gap(15)
            .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                 .Add(Text.H1("Artist Insight Tool"))
                 .Add(new Spacer().Width(Size.Fraction(1)))
                 .Add(new Button("Import Data", () => showImportSheet.Set(true))
                     .Icon(Icons.FileUp)
                     .Variant(ButtonVariant.Primary))
            )
            .Add(Layout.Vertical().Gap(10)
                .Add(globalSearch.ToTextInput().Placeholder("Global search across tables...").Width(Size.Full()))
                .Add(Layout.Horizontal().Gap(2)
                    | tabs.Select((tab, index) =>
                        new Button(tab, _ => selectedTab.Set(index))
                            .Variant(selectedTab.Value == index ? ButtonVariant.Primary : ButtonVariant.Ghost)
                    )
                )
            )
    );

    // --- Tab Content Views ---

    object OverviewTab() => Layout.Vertical().Gap(20)
        .Add(Layout.Grid().Columns(3).Gap(20)
            .Add(new Card(Layout.Center().Add(Text.H2(assets.Count.ToString()))).Title("Total Assets").Description("Number of tracked assets"))
            .Add(new Card(Layout.Center().Add(Text.H2(totalRevenue?.Value ?? "$0.00"))).Title("Total Revenue").Description("Accumulated revenue across all sources"))
            .Add(new Card(Layout.Center().Add(Text.H2(dataTableItems.Count.ToString()))).Title("Data Imports").Description("Number of successful data imports")))
        .Add(new Card(Text.P("Welcome to your Artist Insight Tool dashboard. Use the tabs above to manage your assets, revenue, and data ingestion.")).Title("Quick Start"));

    object AnalyticsTab()
    {
      var categoryData = assets
          .GroupBy(a => a.Category ?? "Uncategorized")
          .Select(g => new CategoryRevenue(g.Key, (double)g.Sum(a => a.AmountGenerated)))
          .ToList();

      var assetBreakdown = assets
          .Where(a => (a.Category ?? "Uncategorized") == selectedCategory.Value)
          .Select(a => new AssetRevenueItem(a.Name, (double)a.AmountGenerated))
          .ToList();

      if (assets.Count == 0) return Layout.Center().Add(Text.Label("No asset data available."));

      return Layout.Vertical().Gap(20)
          .Add(Layout.Horizontal().Gap(20).Width(Size.Full())
              .Add(new Card(categoryData.ToPieChart(e => e.Category, g => g.Sum(f => f.Amount))).Title("Revenue by Category").Width(Size.Fraction(0.5f)))
              .Add(new Card(assetBreakdown.ToPieChart(e => e.Asset, g => g.Sum(f => f.Amount))).Title($"{selectedCategory.Value} Breakdown").Width(Size.Fraction(0.5f))))
          .Add(Layout.Horizontal().Align(Align.Center).Gap(10)
              .Add(Text.Label("Drilldown Category:"))
              .Add(selectedCategory.ToSelectInput(categories.ToOptions()).Width(300)));
    }

    object RevenueTab()
    {
      var filtered = revenueEntries.Where(e =>
          string.IsNullOrWhiteSpace(globalSearch.Value) ||
          (e.Description?.Contains(globalSearch.Value, StringComparison.OrdinalIgnoreCase) ?? false) ||
          (e.Source?.Contains(globalSearch.Value, StringComparison.OrdinalIgnoreCase) ?? false) ||
          (e.Integration?.Contains(globalSearch.Value, StringComparison.OrdinalIgnoreCase) ?? false)
      ).ToList();

      return Layout.Vertical().Gap(20)
          .Add(Layout.Horizontal().Align(Align.Center)
              .Add(Text.H4($"{filtered.Count} Revenue Streams"))
              .Add(new Spacer().Width(Size.Fraction(1)))
              .Add(new Button("Create Entry", () => selectedDialog.Set(new RevenueCreateSheet(() => { selectedDialog.Set(null); revenueQuery.Mutator.Revalidate(); })))
                  .Icon(Icons.Plus).Variant(ButtonVariant.Outline)))
          .Add(filtered.Select(r => new
          {
            Id = $"E{r.Id:D3}",
            Date = r.RevenueDate.ToShortDateString(),
            Name = new Button(r.Description ?? "-", () => selectedDialog.Set(new RevenueViewSheet(r.Id, () => selectedDialog.Set(null), () => selectedDialog.Set(new RevenueEditSheet(r.Id, () => selectedDialog.Set(null))))))
                     .Variant(ButtonVariant.Link),
            Type = r.Source ?? "Unknown",
            Source = r.Integration ?? "Manual",
            Amount = r.Amount.ToString("C", CultureInfo.GetCultureInfo("sv-SE"))
          }).Take(100).ToArray().ToTable()
          .Width(Size.Full())
          .Add(x => x.Id).Add(x => x.Date).Add(x => x.Name).Add(x => x.Type).Add(x => x.Source).Add(x => x.Amount));
    }

    object AssetsTab()
    {
      var filtered = assets.Where(a =>
          string.IsNullOrWhiteSpace(globalSearch.Value) ||
          a.Name.Contains(globalSearch.Value, StringComparison.OrdinalIgnoreCase) ||
          (a.Category?.Contains(globalSearch.Value, StringComparison.OrdinalIgnoreCase) ?? false) ||
          (a.Type?.Contains(globalSearch.Value, StringComparison.OrdinalIgnoreCase) ?? false)
      ).ToList();

      return Layout.Vertical().Gap(20)
          .Add(Layout.Horizontal().Align(Align.Center)
              .Add(Text.H4($"{filtered.Count} Managed Assets"))
              .Add(new Spacer().Width(Size.Fraction(1)))
              .Add(new Button("Create Asset", () => selectedDialog.Set(new CreateAssetSheet(() => { selectedDialog.Set(null); assetsQuery.Mutator.Revalidate(); })))
                  .Icon(Icons.Plus).Variant(ButtonVariant.Outline)))
          .Add(filtered.Select(a => new
          {
            Id = new Button($"A{a.Id:D3}", () => selectedDialog.Set(new AssetViewSheet(a.Id, () => { selectedDialog.Set(null); assetsQuery.Mutator.Revalidate(); }))).Variant(ButtonVariant.Ghost),
            a.Name,
            a.Category,
            a.Type,
            Amount = a.AmountGenerated.ToString("C", CultureInfo.GetCultureInfo("sv-SE")),
            Actions = new Button("", () => selectedDialog.Set(new Dialog(
                  _ => { selectedDialog.Set(null); return ValueTask.CompletedTask; },
                  new DialogHeader("Confirm Deletion"),
                  new DialogBody(Text.Label($"Are you sure you want to delete {a.Name}?")),
                  new DialogFooter(
                      new Button("Cancel", () => selectedDialog.Set(null)),
                      new Button("Delete", async () => { await service.DeleteAssetAsync(a.Id); selectedDialog.Set(null); assetsQuery.Mutator.Revalidate(); }).Variant(ButtonVariant.Destructive))
              ))).Icon(Icons.Trash).Variant(ButtonVariant.Ghost)
          }).Take(100).ToArray().ToTable()
          .Width(Size.Full())
          .Add(x => x.Id).Add(x => x.Name).Add(x => x.Category).Add(x => x.Type).Add(x => x.Amount).Add(x => x.Actions)
          .Header(x => x.Id, "ID").Header(x => x.Actions, ""));
    }

    object DataTablesTab()
    {
      var filtered = dataTableItems.Where(t => string.IsNullOrWhiteSpace(globalSearch.Value) || t.Name.Contains(globalSearch.Value, StringComparison.OrdinalIgnoreCase)).ToList();

      return Layout.Vertical().Gap(20)
          .Add(Text.H4($"{filtered.Count} Linked Data Tables"))
          .Add(filtered.Select(t => new
          {
            Id = new Button(t.Id, () => selectedDialog.Set(new DataTableViewSheet(t.RealId, () => selectedDialog.Set(null)))).Variant(ButtonVariant.Ghost),
            t.Name,
            t.Date,
            Actions = new Button("", () => selectedDialog.Set(new Dialog(
                  _ => { selectedDialog.Set(null); return ValueTask.CompletedTask; },
                  new DialogHeader("Confirm Deletion"),
                  new DialogBody(Text.Label($"Are you sure you want to delete {t.Name}?")),
                  new DialogFooter(
                      new Button("Cancel", () => selectedDialog.Set(null)),
                      new Button("Delete", async () => { await service.DeleteRevenueEntryAsync(t.RealId); selectedDialog.Set(null); revenueQuery.Mutator.Revalidate(); }).Variant(ButtonVariant.Destructive))
              ))).Icon(Icons.Trash).Variant(ButtonVariant.Ghost)
          }).Take(100).ToArray().ToTable()
          .Width(Size.Full())
          .Add(x => x.Id).Add(x => x.Name).Add(x => x.Date).Add(x => x.Actions)
          .Header(x => x.Id, "ID").Header(x => x.Actions, ""));
    }

    object TemplatesTab()
    {
      var filtered = templatesData.Where(t =>
          string.IsNullOrWhiteSpace(globalSearch.Value) ||
          t.Name.Contains(globalSearch.Value, StringComparison.OrdinalIgnoreCase) ||
          t.Category.Contains(globalSearch.Value, StringComparison.OrdinalIgnoreCase)
      ).ToList();

      return Layout.Vertical().Gap(20)
          .Add(Layout.Horizontal().Align(Align.Center)
              .Add(Text.H4($"{filtered.Count} Active Templates"))
              .Add(new Spacer().Width(Size.Fraction(1)))
              .Add(new Button("New Template", () => selectedDialog.Set(new CreateTemplateSheet(() => { selectedDialog.Set(null); tmplQuery.Mutator.Revalidate(); })))
                  .Icon(Icons.Plus).Variant(ButtonVariant.Outline)))
          .Add(filtered.Select(t => new
          {
            Id = new Button(t.Id, () => selectedDialog.Set(new TemplateEditSheet(() => selectedDialog.Set(null), t.RealId, client))).Variant(ButtonVariant.Ghost),
            t.Name,
            t.Category,
            Linked = t.Files,
            Actions = new Button("", () => selectedDialog.Set(new Dialog(
                  _ => { selectedDialog.Set(null); return ValueTask.CompletedTask; },
                  new DialogHeader("Confirm Deletion"),
                  new DialogBody(Text.Label($"Are you sure you want to delete {t.Name}?")),
                  new DialogFooter(
                      new Button("Cancel", () => selectedDialog.Set(null)),
                      new Button("Delete", async () => { await service.DeleteTemplateAsync(t.RealId); selectedDialog.Set(null); tmplQuery.Mutator.Revalidate(); }).Variant(ButtonVariant.Destructive))
              ))).Icon(Icons.Trash).Variant(ButtonVariant.Ghost)
          }).Take(100).ToArray().ToTable()
          .Width(Size.Full())
          .Add(x => x.Id).Add(x => x.Name).Add(x => x.Category).Add(x => x.Linked).Add(x => x.Actions)
          .Header(x => x.Id, "ID").Header(x => x.Linked, "Files").Header(x => x.Actions, ""));
    }

    var contentArea = Layout.Vertical().Height(Size.Full()).Padding(20);
    if (assetsQuery.Loading || revenueQuery.Loading || totalRevenueQuery.Loading || tmplQuery.Loading)
    {
      contentArea.Add(Layout.Center().Add(Text.Label("Syncing and analyzing data...")));
    }
    else
    {
      contentArea.Add(selectedTab.Value switch
      {
        0 => OverviewTab(),
        1 => AnalyticsTab(),
        2 => RevenueTab(),
        3 => AssetsTab(),
        4 => DataTablesTab(),
        5 => TemplatesTab(),
        _ => OverviewTab()
      });
    }

    return new Fragment(
        new HeaderLayout(headerCard, contentArea),
        selectedDialog.Value
    );
  }
}
