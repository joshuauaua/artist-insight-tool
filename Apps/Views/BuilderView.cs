using Ivy.Shared;
using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps.Views;

public class BuilderView : WrapperViewBase
{
  record BlockDef(string Id, string Label, string Description, Icons Icon, string Category);

  static readonly BlockDef[] AvailableBlocks =
  {
        // Events
        new("streams",         "Streams",         "Streaming activity for assets.", Icons.Music, "EVENT"),
        new("sales",           "Sales",           "Physical or digital sales events.", Icons.ShoppingCart, "EVENT"),
        new("royalty_payment", "Royalty Payment", "Incoming royalty distributions.", Icons.CircleDollarSign, "EVENT"),
        new("sync_license",    "Sync License",    "Licensing for film/TV/games.", Icons.Videotape, "EVENT"),
        
        // Filters
        new("filter_asset",    "Asset Filter",    "Filter by specific track or album.", Icons.Package, "FILTER"),
        new("filter_source",   "Source Filter",   "Filter by DSP (Spotify, Apple, etc).", Icons.Globe, "FILTER"),
        new("filter_region",   "Region Filter",   "Filter by geographic region.", Icons.MapPin, "FILTER"),
        new("filter_amount",   "Amount Filter",   "Filter by revenue threshold.", Icons.DollarSign, "FILTER"),
    };

  public override object? Build()
  {
    var canvasBlocks = UseState(Array.Empty<string>());
    var operators = UseState(Array.Empty<string>());
    var categoryFilter = UseState("All");
    var cohortName = UseState("New Cohort");
    var isEditingName = UseState(false);
    var client = UseService<IClientProvider>();

    // Mock counts
    var audienceCount = canvasBlocks.Value.Length > 0 ? new Random().Next(100, 5000) : 0;
    var activityCount = canvasBlocks.Value.Length > 0 ? (int)(audienceCount * 0.4) : 0;

    var titleContent = isEditingName.Value
        ? Layout.Horizontal().Gap(2).Align(Align.Left)
            | new TextInput(cohortName, placeholder: "Cohort name...")
            | new Button().Icon(Icons.Check).Variant(ButtonVariant.Ghost).HandleClick(_ => isEditingName.Set(false))
        : Layout.Horizontal().Gap(2).Align(Align.Left)
            | Text.H3(cohortName.Value)
            | new Button().Icon(Icons.Pencil).Variant(ButtonVariant.Ghost).HandleClick(_ => isEditingName.Set(true));

    var headerCard = new Card(
        Layout.Vertical().Gap(4).Align(Align.Left).Width(Size.Full())
            | (Layout.Vertical().Gap(1).Align(Align.Left)
                | titleContent
                | Text.P("Define your artist cohorts by combining events and filters.").Muted())
            | (Layout.Horizontal().Gap(6).Align(Align.Center).Width(Size.Full())
                | (Layout.Horizontal().Gap(2)
                    | new Button("Save Cohort").Variant(ButtonVariant.Primary).Icon(Icons.Save).HandleClick(_ => client.Toast("Cohort Saved!"))
                    | new Button("Export").Variant(ButtonVariant.Outline).Icon(Icons.Download).HandleClick(_ => client.Toast("Exporting..."))
                    | new Button("Clear").Variant(ButtonVariant.Destructive).Icon(Icons.Trash2).HandleClick(_ => { canvasBlocks.Set([]); operators.Set([]); }))
                | new Spacer()
                | new Card(
                    Layout.Horizontal().Gap(6).Align(Align.Center).Padding(2, 6)
                        | (Layout.Vertical().Gap(1).Align(Align.Center)
                            | Text.H2(audienceCount > 0 ? audienceCount.ToString("N0") : "—").Color(Colors.Blue)
                            | Text.P("Matching Assets").Muted().Small())
                        | Layout.Vertical().Width(Size.Units(1)).Height(Size.Units(40)).Background(Colors.Slate)
                        | (Layout.Vertical().Gap(1).Align(Align.Center)
                            | Text.H2(activityCount > 0 ? activityCount.ToString("N0") : "—").Color(Colors.Green)
                            | Text.P("Recent Events").Muted().Small())
                )
            )
    );

    // Sidebar content
    var sidebarContent = Layout.Vertical().Gap(2)
        | AvailableBlocks
            .Where(b => (categoryFilter.Value == "All" || b.Category == categoryFilter.Value) && !canvasBlocks.Value.Contains(b.Id))
            .Select(b =>
                (object)new Card(
                    Layout.Horizontal().Gap(2).Align(Align.Center).Padding(2, 4)
                        | (Layout.Vertical().Gap(1)
                            | Text.P(b.Label).Small().Bold()
                            | Text.P(b.Category).Small().Muted().Color(Colors.Purple))
                        | new Spacer()
                        | new Button("+")
                            .Variant(ButtonVariant.Ghost)
                            .HandleClick(_ =>
                            {
                              if (canvasBlocks.Value.Length >= 1)
                              {
                                operators.Set([.. operators.Value, "AND"]);
                              }
                              canvasBlocks.Set([.. canvasBlocks.Value, b.Id]);
                            })
                ).WithTooltip(b.Description));

    // Canvas items logic
    IEnumerable<object> canvasItems = canvasBlocks.Value.Select<string, object[]>((blockId, idx) =>
    {
      var def = AvailableBlocks.FirstOrDefault(b => b.Id == blockId)
                                ?? new BlockDef(blockId, blockId, "", Icons.Box, "EVENT");
      var capturedIdx = idx;

      var blockCard = (object)(Layout.Horizontal().Width(Size.Units(100)).Align(Align.Center)
              | new Card(
                  Layout.Horizontal().Gap(4).Align(Align.Center).Padding(4)
                      | (Layout.Vertical().Gap(0)
                          | Text.P(def.Label).Bold()
                          | Text.P(def.Category).Small().Muted().Color(Colors.Purple))
                      | new Spacer()
                      | new Button().Icon(Icons.X).Variant(ButtonVariant.Ghost)
                          .HandleClick(_ =>
                          {
                            if (capturedIdx > 0 && operators.Value.Length > capturedIdx - 1)
                            {
                              operators.Set(operators.Value.Where((_, i) => i != capturedIdx - 1).ToArray());
                            }
                            else if (operators.Value.Length > 0)
                            {
                              operators.Set(operators.Value.Skip(1).ToArray());
                            }
                            canvasBlocks.Set(canvasBlocks.Value.Where((_, i) => i != capturedIdx).ToArray());
                          })));

      if (idx > 0 && idx - 1 < operators.Value.Length)
      {
        var opIdx = idx - 1;
        var op = operators.Value[opIdx];
        var opSelector = (object)(Layout.Horizontal().Gap(2).Align(Align.Center)
                | new Button("AND")
                    .Variant(op == "AND" ? ButtonVariant.Primary : ButtonVariant.Outline)
                    .HandleClick(_ =>
                    {
                      var newOps = operators.Value.ToArray();
                      newOps[opIdx] = "AND";
                      operators.Set(newOps);
                    })
                | new Button("OR")
                    .Variant(op == "OR" ? ButtonVariant.Primary : ButtonVariant.Outline)
                    .HandleClick(_ =>
                    {
                      var newOps = operators.Value.ToArray();
                      newOps[opIdx] = "OR";
                      operators.Set(newOps);
                    }));

        return [opSelector, blockCard];
      }

      return [blockCard];
    }).SelectMany(x => x);

    object canvasInner = canvasBlocks.Value.Length == 0
        ? Layout.Center()
            | (Layout.Vertical().Gap(6).Align(Align.Center)
                | new Icon(Icons.LayoutTemplate).Size(16).Color(Colors.Slate)
                | Text.H3("Builder Canvas")
                | Text.P("Click '+' on a sidebar block to build your cohort.").Muted())
        : Layout.Vertical().Gap(4).Align(Align.Center).Width(Size.Half())
            | canvasItems;

    var canvasCard = new Card(
        Layout.Vertical().Align(Align.Center).Padding(10).Height(Size.Units(120))
            | canvasInner
    );

    // Insights content
    object? cohortInsights = null;
    if (canvasBlocks.Value.Length > 0)
    {
      var demoData = new[]
      {
                new { Source = "Spotify",  Value = 45 },
                new { Source = "Apple Music", Value = 30 },
                new { Source = "Amazon", Value = 15 },
                new { Source = "Others",   Value = 10 },
            };

      var eventsData = new[]
      {
                new { Type = "Streams",    Count = 12500 },
                new { Type = "Sales",      Count = 450 },
                new { Type = "Licensing",  Count = 12 },
                new { Type = "Royalty",    Count = 85 },
            };

      cohortInsights = Layout.Vertical().Gap(4)
          | (Layout.Vertical().Gap(1)
              | Text.H3("Cohort Insights")
              | Text.P("Performance breakdown for your selected cohort.").Muted())
          | (Layout.Grid().Columns(2).Gap(4)
              | new Card(
                  Layout.Vertical().Gap(2)
                      | Text.P("Revenue by Source").Bold()
                      | demoData.ToPieChart(e => e.Source, e => (double)e.Sum(f => f.Value)))
              | new Card(
                  Layout.Vertical().Gap(2)
                      | Text.P("Activity Breakdown").Bold()
                      | eventsData.ToBarChart()
                          .Dimension("Event", e => e.Type)
                          .Measure("Count", e => (double)e.Sum(f => f.Count))));
    }

    var mainContent = Layout.Vertical().Gap(4).Width(Size.Full())
        | headerCard
        | canvasCard
        | cohortInsights;

    return new SidebarLayout(
        mainContent: mainContent,
        sidebarContent: sidebarContent,
        sidebarHeader: Layout.Vertical().Gap(2)
            | Text.Lead("Available Blocks")
            | new SelectInput<string>(categoryFilter, new[] { "All", "EVENT", "FILTER" }.ToOptions())
    );
  }
}
