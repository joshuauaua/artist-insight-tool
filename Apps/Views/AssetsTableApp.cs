using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ArtistInsightTool.Apps.Views;

[App(icon: Icons.Table, title: "Assets Table", path: ["Data", "Assets"])]
public class AssetsTableApp : ViewBase
{
  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var assets = UseState<Asset[]>([]);
    var showCreate = UseState(false);
    var showSpotifyImport = UseState(false);
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

    if (showSpotifyImport.Value)
    {
      return new ImportSpotifyAssetSheet(() =>
      {
        showSpotifyImport.Set(false);
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
      Delete = new Button("", async () => await DeleteAsset(a.Id)).Icon(Icons.Trash).Variant(ButtonVariant.Destructive)
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
        .Padding(0) // Reset outer padding to control it in sections
        .Gap(0)
        .Add(Layout.Vertical()
            .Width(Size.Full())
            .Height(Size.Fit())
            .Gap(10)
            .Padding(20, 20, 20, 5)
            .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                .Add("Assets")
                .Add(new Spacer().Width(Size.Fraction(1)))
                .Add(new Button("Fetch from Spotify", () => showSpotifyImport.Set(true)).Icon(Icons.CloudDownload).Variant(ButtonVariant.Outline))
                .Add(new Button("Create Asset", () => showCreate.Set(true)).Icon(Icons.Plus).Variant(ButtonVariant.Primary))
            )
        )
        .Add(Layout.Vertical().Height(Size.Fraction(1))
             .Add(table)
        );
  }
}

public class ImportSpotifyAssetSheet(Action onClose) : ViewBase
{
  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var artistId = UseState("");
    var token = UseState("");
    var isImporting = UseState(false);

    return new Dialog(
        _ => onClose(),
        new DialogHeader("Fetch Asset from Spotify"),
        new DialogBody(
            Layout.Vertical().Gap(10)
            .Add(Text.Label("Spotify Artist ID"))
            .Add(artistId.ToTextInput().Placeholder("e.g. 0TnOYISbd1XYRBk9myaseg"))
            .Add(Text.Label("Bearer Token"))
            .Add(token.ToTextInput().Placeholder("Authorization: Bearer ..."))
            .Add(Layout.Horizontal().Align(Align.Right).Gap(10).Padding(10, 0, 0, 0)
                .Add(new Button("Cancel", onClose))
                .Add(new Button("Fetch & Create", async () =>
                {
                  if (string.IsNullOrWhiteSpace(artistId.Value) || string.IsNullOrWhiteSpace(token.Value))
                  {
                    // Error: Artist ID and Token are required.
                    return;
                  }

                  isImporting.Set(true);
                  // error cleared

                  try
                  {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

                    var url = $"https://api.spotify.com/v1/artists/{artistId.Value}";
                    var response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                      // Error: API Error
                      isImporting.Set(false);
                      return;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("name", out var nameProp))
                    {
                      var artistName = nameProp.GetString();
                      if (!string.IsNullOrEmpty(artistName))
                      {
                        await using var db = factory.CreateDbContext();
                        db.Assets.Add(new Asset
                        {
                          Name = artistName,
                          Type = "Artist",
                          AmountGenerated = 0
                        });
                        await db.SaveChangesAsync();
                        onClose();
                      }
                    }
                    else
                    {
                      // Error: Could not parse name
                    }
                  }
                  catch (Exception)
                  {
                    // Error: Exception
                  }
                  isImporting.Set(false);
                }).Variant(ButtonVariant.Primary).Disabled(isImporting.Value))
            )
        ),
        new DialogFooter()
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
