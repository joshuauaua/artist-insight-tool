using System.Globalization;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Ivy.Shared;

namespace ArtistInsightTool.Apps.Views;

public class RevenueEditSheet(int id, Action onClose) : ViewBase
{
  private readonly int _id = id;
  private readonly Action _onClose = onClose;

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var entryState = UseState<RevenueEntry?>(() => null);

    // Form States
    var amountState = UseState("");
    var descriptionState = UseState("");
    var dateState = UseState(DateTime.Now);
    var dateStringState = UseState(""); // String state for input binding

    async Task<IDisposable?> LoadEntry()
    {
      await using var db = factory.CreateDbContext();
      var data = await db.RevenueEntries
          .Include(e => e.Artist)
          .Include(e => e.Source)
          .Include(e => e.Album)
          .Include(e => e.Track).ThenInclude(t => t.Album)
          .FirstOrDefaultAsync(e => e.Id == _id);

      if (data != null)
      {
        entryState.Set(data);
        amountState.Set(data.Amount.ToString(CultureInfo.InvariantCulture)); // Use invariant for editing
        descriptionState.Set(data.Description ?? "");
        dateState.Set(data.RevenueDate);
        dateStringState.Set(data.RevenueDate.ToString("MM/dd/yyyy"));
      }
      return null;
    }

    UseEffect(LoadEntry, [EffectTrigger.AfterInit()]);

    if (entryState.Value is null) return "Loading...";

    var e = entryState.Value;

    var name = e.Track?.Title ?? e.Album?.Title ?? "-";
    var type = e.Track != null ? "Track" : (e.Album != null ? "Album" : "Other");

    return Layout.Vertical()
        .Gap(20)
        .Padding(20)
        .Add(Layout.Horizontal().Align(Align.Center)
            .Add(new Button("← Back", _onClose).Variant(ButtonVariant.Primary))
        )
        .Add(new Card(
            Layout.Vertical().Gap(15)
                .Add(Layout.Vertical().Gap(5)
                    .Add("Entry Details")
                    .Add($"{e.Source.DescriptionText} • {type}")
                    .Add(name)
                )
                .Add(Layout.Vertical().Gap(15)
                    .Add(Layout.Vertical().Gap(5)
                        .Add("Amount ($)")
                        .Add(amountState.ToTextInput().Placeholder("0.00"))
                    )
                     .Add(Layout.Vertical().Gap(5)
                        .Add("Date (MM/dd/yyyy)")
                        .Add(dateStringState.ToTextInput())
                    )
                    .Add(Layout.Vertical().Gap(5)
                        .Add("Description")
                        .Add(descriptionState.ToTextInput().Placeholder("Enter description..."))
                    )
                )
        ).Title("Edit Entry"))
        .Add(Layout.Horizontal().Width(Size.Full()).Align(Align.Right).Gap(10)
             .Add(new Button("Delete", async () =>
             {
               await using var db = factory.CreateDbContext();
               var entry = await db.RevenueEntries.FindAsync(_id);
               if (entry != null)
               {
                 db.RevenueEntries.Remove(entry);
                 await db.SaveChangesAsync();
               }
               _onClose();
             }).Variant(ButtonVariant.Destructive))
             .Add(new Button("Save Changes", async () =>
             {
               if (decimal.TryParse(amountState.Value, out var newAmount))
               {
                 await using var db = factory.CreateDbContext();
                 var entry = await db.RevenueEntries.FindAsync(_id);
                 if (entry != null)
                 {
                   entry.Amount = newAmount;
                   entry.Description = descriptionState.Value;
                   if (DateTime.TryParse(dateStringState.Value, out var newDate))
                   {
                     entry.RevenueDate = newDate;
                   }
                   await db.SaveChangesAsync();
                 }
                 _onClose();
               }
             }))
        );
  }
}
