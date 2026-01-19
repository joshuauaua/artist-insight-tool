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

    async Task<IDisposable?> LoadAsset()
    {
      await using var db = factory.CreateDbContext();
      var data = await db.Assets
          .Include(a => a.AssetRevenues)
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
        // Actions
        // Actions
        .Add(Layout.Horizontal().Align(Align.Center).Gap(10).Padding(10, 0, 0, 0)
             .Add(new Button("Extended Details", () => { /* TODO: Implement navigation */ }).Variant(ButtonVariant.Primary))
        );
  }
}
