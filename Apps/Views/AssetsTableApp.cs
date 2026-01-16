using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ArtistInsightTool.Apps.Views;

[App(icon: Icons.Table, title: "Assets Table", path: ["Data", "Assets"])]
public class AssetsTableApp : ViewBase
{
  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var assets = UseState<Asset[]>([]);
    var showCreate = UseState(false);
    var refreshToken = UseState(0);

    async Task<IDisposable?> LoadData()
    {
      await using var db = factory.CreateDbContext();
      var data = await db.Assets
          .Include(a => a.AssetRevenues)
          .Select(a => new Asset
          {
            Id = a.Id,
            Name = a.Name,
            Type = a.Type,
            AmountGenerated = a.AssetRevenues.Sum(ar => ar.Amount)
          })
          .OrderBy(a => a.Name)
          .ToArrayAsync();
      assets.Set(data);
      return null;
    }

    UseEffect(LoadData, [EffectTrigger.AfterInit(), refreshToken]);

    if (showCreate.Value)
    {
      return new CreateAssetSheet(() =>
      {
        showCreate.Set(false);
        refreshToken.Set(refreshToken.Value + 1);
      });
    }

    async Task DeleteAsset(int id)
    {
      await using var db = factory.CreateDbContext();
      var asset = await db.Assets.FindAsync(id);
      if (asset != null)
      {
        db.Assets.Remove(asset);
        await db.SaveChangesAsync();
        refreshToken.Set(refreshToken.Value + 1);
      }
    }

    var table = assets.Value.Select(a => new
    {
      a.Id,
      a.Name,
      a.Type,
      Amount = a.AmountGenerated.ToString("C"),
      Delete = new Button("", () => DeleteAsset(a.Id)).Icon(Icons.Trash).Variant(ButtonVariant.Destructive)
    }).ToArray()
    .ToTable()
    .Width(Size.Full())
    .Header(x => x.Id, "ID")
    .Header(x => x.Name, "Asset Name")
    .Header(x => x.Type, "Type")
    .Header(x => x.Amount, "Amount Generated")
    .Header(x => x.Delete, "Delete");

    return Layout.Vertical()
        .Height(Size.Full())
        .Padding(20)
        .Gap(20)
        .Add(Layout.Horizontal().Align(Align.Center)
            .Add(Text.H4("Assets"))
            .Add(new Spacer())
            .Add(new Button("Create Asset", () => showCreate.Set(true)).Variant(ButtonVariant.Primary))
        )
        .Add(Layout.Vertical().Height(Size.Fraction(1))
             .Add(table)
        );
  }
}

public class CreateAssetSheet(Action onClose) : ViewBase
{
  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var name = UseState("");
    var type = UseState("");

    return new Dialog(
        _ => onClose(),
        new DialogHeader("Create Asset"),
        new DialogBody(
            Layout.Vertical().Gap(10)
            .Add(Text.Label("Name"))
            .Add(name.ToTextInput().Placeholder("Asset Name"))
            .Add(Text.Label("Type"))
            .Add(type.ToTextInput().Placeholder("e.g. Master, Publishing"))
            .Add(Layout.Horizontal().Align(Align.Right).Gap(10).Padding(10, 0, 0, 0)
                .Add(new Button("Cancel", onClose))
                .Add(new Button("Create", async () =>
                {
                  if (string.IsNullOrEmpty(name.Value)) return;

                  await using var db = factory.CreateDbContext();
                  db.Assets.Add(new Asset
                  {
                    Name = name.Value,
                    Type = type.Value,
                    AmountGenerated = 0 // Calculated dynamically later
                  });
                  await db.SaveChangesAsync();
                  onClose();
                }).Variant(ButtonVariant.Primary))
            )
        ),
        new DialogFooter()
    );
  }
}
