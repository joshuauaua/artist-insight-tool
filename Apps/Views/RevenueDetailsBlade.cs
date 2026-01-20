using ArtistInsightTool.Connections.ArtistInsightTool;
using Ivy.Shared;

namespace ArtistInsightTool.Apps.Views;

public class RevenueDetailsBlade(int id) : ViewBase
{
  private readonly int _id = id;

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var entryState = UseState<RevenueEntry?>(() => null);

    async Task<IDisposable?> LoadEntry()
    {
      await using var db = factory.CreateDbContext();
      var data = await db.RevenueEntries
         .Include(e => e.Artist)
         .Include(e => e.Source)

         .FirstOrDefaultAsync(e => e.Id == _id);
      entryState.Set(data);
      return null;
    }

    UseEffect(LoadEntry, [EffectTrigger.AfterInit()]);

    if (entryState.Value is null) return "Loading...";

    var e = entryState.Value;

    var amount = e.Amount.ToString("C", CultureInfo.CurrentCulture);

    return Layout.Vertical()
        .Gap(15)
        .Padding(20)
        .Add(e.RevenueDate.ToShortDateString())
        .Add(amount)
        .Add(Layout.Vertical().Gap(5)
            .Add("Source")
            .Add(e.Source.DescriptionText)
        )
        .Add(Layout.Vertical().Gap(5)
            .Add("Type")
            .Add("Other")
        )
        .Add(Layout.Vertical().Gap(5)
            .Add("Name")
            .Add("-")
        )
        .Add(Layout.Vertical().Gap(5)
            .Add("Description")
            .Add(e.Description ?? "-")
        );
  }
}
