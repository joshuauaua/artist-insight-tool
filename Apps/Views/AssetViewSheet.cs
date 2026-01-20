using System.Globalization;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Ivy.Shared;

namespace ArtistInsightTool.Apps.Views;

public class AssetViewSheet(int id, Action onClose) : ViewBase
{
  private readonly int _id = id;
  private readonly Action _onClose = onClose;

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var assetState = UseState<Asset?>(() => null);
    var isDeleting = UseState(false);

    async Task<IDisposable?> LoadAsset()
    {
      await using var db = factory.CreateDbContext();
      var data = await db.Assets
          .Include(a => a.AssetRevenues)
          .ThenInclude(ar => ar.RevenueEntry)
          .FirstOrDefaultAsync(a => a.Id == _id);

      if (data != null)
      {
        assetState.Set(data);
      }
      return null;
    }

    UseEffect(LoadAsset, [EffectTrigger.AfterInit()]);

    if (assetState.Value is null) return Layout.Vertical().Gap(10).Add(Text.Muted("Loading..."));

    var a = assetState.Value;
    var totalAmount = a.AssetRevenues.Sum(ar => ar.Amount);

    return Layout.Vertical().Gap(10)
        // 1. Name & Description (Category/Type as sub-header effectively)
        .Add(Text.H4(a.Name))

        // 2. Info Card
        .Add(new Card(
             Layout.Vertical().Gap(10)
                 // Row 1: Category & Type
                 .Add(Layout.Horizontal().Gap(10).Align(Align.Center)
                     .Add(Layout.Vertical().Gap(2)
                         .Add(Text.Label("Category"))
                         .Add(Text.Muted(a.Category))
                     )
                     .Add(new Spacer())
                     .Add(Layout.Vertical().Gap(2).Align(Align.Center)
                         .Add(Text.Label("Type"))
                         .Add(Text.Muted(a.Type))
                     )
                 )
                 // Row 2: Collection & Amount
                 .Add(Layout.Horizontal().Gap(10).Align(Align.Center)
                    .Add(Layout.Vertical().Gap(2)
                        .Add(Text.Label("Collection"))
                        .Add(Text.Muted(a.Collection ?? "-"))
                    )
                    .Add(new Spacer())
                    .Add(Layout.Vertical().Gap(2).Align(Align.Center)
                        .Add(Text.Label("Total Generated"))
                        .Add(Text.Muted(totalAmount.ToString("C", CultureInfo.GetCultureInfo("sv-SE"))))
                    )
                )
        ))
         // 3. Revenues Table
         .Add(Text.H5("Revenue History"))
         .Add(
             a.AssetRevenues
             .OrderByDescending(ar => ar.RevenueEntry?.RevenueDate)
             .Select(ar => new
             {
               Date = ar.RevenueEntry?.RevenueDate.ToShortDateString() ?? "-",
               Entry = ar.RevenueEntry?.Description ?? "Unknown Entry",
               Source = ar.RevenueEntry?.Integration ?? "Direct",
               Amount = ar.Amount.ToString("C", CultureInfo.GetCultureInfo("sv-SE"))
             })
             .ToTable()
             .Width(Size.Full())
             .Header(x => x.Date, "Date")
             .Header(x => x.Entry, "Entry Name")
             .Header(x => x.Source, "Source")
             .Header(x => x.Amount, "Amount")
         )

        // Actions
        .Add(Layout.Horizontal().Align(Align.Center).Gap(10).Padding(10, 0, 0, 0)
             .Add(new Button(isDeleting.Value ? "Confirm Delete?" : "Delete", async () =>
             {
               if (!isDeleting.Value)
               {
                 isDeleting.Set(true);
                 return;
               }

               await using var db = factory.CreateDbContext();
               var assetToDelete = await db.Assets.FindAsync(a.Id);
               if (assetToDelete != null)
               {
                 db.Assets.Remove(assetToDelete);
                 await db.SaveChangesAsync();
               }
               _onClose();
             }).Variant(ButtonVariant.Destructive))
             .Add(isDeleting.Value ? new Button("Cancel", () => isDeleting.Set(false)).Variant(ButtonVariant.Ghost) : null)
             .Add(new Button("Close", _onClose).Variant(ButtonVariant.Primary))
        );
  }
}
