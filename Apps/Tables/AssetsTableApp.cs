using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;
using ArtistInsightTool.Apps.Views;
using ArtistInsightTool.Apps.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Globalization;

namespace ArtistInsightTool.Apps.Tables;

[App(icon: Icons.Table, title: "Assets Table", path: ["Tables"])]
public class AssetsTableApp : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var factory = UseService<ArtistInsightToolContextFactory>();
    var assets = UseState<Asset[]>([]);
    var showCreate = UseState(false);
    var showSpotifyImport = UseState(false);
    var selectedAssetId = UseState<int?>(() => null);
    var confirmDeleteId = UseState<int?>(() => null);
    var refreshToken = UseState(0);

    async Task<IDisposable?> LoadData()
    {
      var data = await service.GetAssetsAsync();
      assets.Set(data.OrderBy(a => a.Id).ToArray());
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



    // Search State
    var searchQuery = UseState("");

    // Filter Logic
    var filteredAssets = assets.Value.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(searchQuery.Value))
    {
      var q = searchQuery.Value.ToLowerInvariant();
      filteredAssets = filteredAssets.Where(a =>
          a.Name.ToLowerInvariant().Contains(q) ||
          a.Category.ToLowerInvariant().Contains(q) ||
          a.Type.ToLowerInvariant().Contains(q) ||
          (a.Collection != null && a.Collection.ToLowerInvariant().Contains(q))
      );
    }

    var table = filteredAssets.Select(a => new
    {
      IdButton = new Button($"A{a.Id:D3}", () => selectedAssetId.Set(a.Id)).Variant(ButtonVariant.Ghost),
      a.Name,
      a.Category,
      a.Type,
      a.Collection,
      Amount = a.AmountGenerated.ToString("C", CultureInfo.GetCultureInfo("sv-SE")),
      Actions = new Button("", () => confirmDeleteId.Set(a.Id)).Icon(Icons.Trash).Variant(ButtonVariant.Ghost)
    }).ToArray()
    .ToTable()
    .Width(Size.Full())
    .Add(x => x.IdButton)
    .Add(x => x.Name)
    .Add(x => x.Category)
    .Add(x => x.Type)
    .Add(x => x.Collection)
    .Add(x => x.Amount)
    .Add(x => x.Actions)
    .Header(x => x.IdButton, "ID")
    .Header(x => x.Name, "Asset Name")
    .Header(x => x.Category, "Category")
    .Header(x => x.Type, "Type")
    .Header(x => x.Collection, "Collection")
    .Header(x => x.Amount, "Amount Generated")
    .Header(x => x.Actions, "");

    var headerContent = Layout.Vertical()
        .Width(Size.Full())
        .Height(Size.Fit())
        .Gap(10)
        .Padding(20, 20, 20, 5)
        .Add(Layout.Horizontal().Width(Size.Full()).Height(Size.Fit()).Align(Align.Center)
             .Add("Assets Table")
             .Add(new Spacer().Width(Size.Fraction(1)))
             .Add(new DropDownMenu(
                     DropDownMenu.DefaultSelectHandler(),
                     new Button("Create Asset").Icon(Icons.Plus).Variant(ButtonVariant.Primary)
                 )
                 | MenuItem.Default("Manual Entry").Icon(Icons.Plus)
                     .HandleSelect(() => showCreate.Set(true))
                 | MenuItem.Default("Fetch from Spotify").Icon(Icons.CloudDownload)
                     .HandleSelect(() => showSpotifyImport.Set(true))
             )
        )
        .Add(Layout.Horizontal().Width(Size.Full()).Height(Size.Fit()).Gap(10)
             .Add(searchQuery.ToTextInput().Placeholder("Search assets...").Width(300))
        );

    return new Fragment(
        Layout.Vertical()
            .Height(Size.Full())
            .Gap(0)
            .Add(headerContent)
            .Add(Layout.Vertical().Height(Size.Fraction(1)).Padding(20, 0, 20, 50)
                 .Add(table)
            ),
        selectedAssetId.Value != null ? new Dialog(
            _ => selectedAssetId.Set((int?)null),
            new DialogHeader("Asset Details"),
            new DialogBody(new AssetViewSheet(selectedAssetId.Value.Value, () => selectedAssetId.Set((int?)null))),
            new DialogFooter()
        ) : null,
        confirmDeleteId.Value != null ? new Dialog(
            _ => confirmDeleteId.Set((int?)null),
            new DialogHeader("Confirm Deletion"),
            new DialogBody(Text.Label("Are you sure you want to delete this asset? This action cannot be undone.")),
            new DialogFooter(
                new Button("Cancel", () => { confirmDeleteId.Set((int?)null); }),
                new Button("Delete", async () =>
                {
                  if (confirmDeleteId.Value == null) return;
                  var success = await service.DeleteAssetAsync(confirmDeleteId.Value.Value);
                  if (success)
                  {
                    refreshToken.Set(refreshToken.Value + 1);
                  }
                  confirmDeleteId.Set((int?)null);
                }).Variant(ButtonVariant.Destructive))
        ) : null
    );
  }
}

public class ImportSpotifyAssetSheet(Action onClose) : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var importType = UseState("Artist");
    var id = UseState("");
    var token = UseState("");
    var isImporting = UseState(false);

    return new Dialog(
        _ => onClose(),
        new DialogHeader("Fetch Asset from Spotify"),
        new DialogBody(
            Layout.Vertical().Gap(10)
            .Add(Text.Label("Import Type"))
            .Add(importType.ToSelectInput([new Option<string>("Artist", "Artist"), new Option<string>("Song", "Song")]))
            .Add(Text.Label("Spotify ID"))
            .Add(id.ToTextInput().Placeholder("e.g. 0TnOYISbd1XYRBk9myaseg"))
            .Add(Text.Label("Bearer Token"))
            .Add(token.ToTextInput().Placeholder("Authorization: Bearer ..."))
            .Add(Layout.Horizontal().Align(Align.Right).Gap(10).Padding(10, 0, 0, 0)
                .Add(new Button("Cancel", onClose))
                .Add(new Button("Fetch & Create", async () =>
                {
                  if (string.IsNullOrWhiteSpace(id.Value) || string.IsNullOrWhiteSpace(token.Value))
                  {
                    // Error: ID and Token are required.
                    return;
                  }

                  isImporting.Set(true);

                  try
                  {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

                    string endpoint = importType.Value == "Artist" ? "artists" : "tracks";
                    var url = $"https://api.spotify.com/v1/{endpoint}/{id.Value}";
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
                      var assetName = nameProp.GetString();
                      if (!string.IsNullOrEmpty(assetName))
                      {
                        await service.CreateAssetAsync(new Asset
                        {
                          Name = assetName,
                          Type = importType.Value, // "Artist" or "Song"
                          AmountGenerated = 0
                        });
                        onClose();
                      }
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
    var service = UseService<ArtistInsightService>();
    var name = UseState("");
    var category = UseState("Royalties");
    var type = UseState("");
    var collection = UseState("");

    var typeOptions = category.Value switch
    {
      "Concert" => new[] { "Single Concert", "Tour" },
      "Merchandise" => new[] { "Item", "Collection" },
      "Royalties" => new[] { "Single", "EP", "Album" },
      _ => Array.Empty<string>()
    };

    UseEffect(() =>
    {
      if (category.Value == "Other")
      {
        type.Set("");
      }
      else if (typeOptions.Length > 0 && !typeOptions.Contains(type.Value))
      {
        type.Set(typeOptions[0]);
      }
    }, [category]);

    return new Dialog(
        _ => onClose(),
        new DialogHeader("Create Asset"),
        new DialogBody(
            Layout.Vertical().Gap(10)
            .Add(Text.Label("Name"))
            .Add(name.ToTextInput().Placeholder("Asset Name"))
            .Add(Text.Label("Category"))
            .Add(category.ToSelectInput(new[] { "Concert", "Merchandise", "Royalties", "Other" }.Select(c => new Option<string>(c, c))))
            .Add(Text.Label("Type"))
            .Add(category.Value == "Other"
                ? type.ToTextInput().Placeholder("Type")
                : type.ToSelectInput(typeOptions.Select(t => new Option<string>(t, t))))
            .Add(Text.Label("Collection"))
            .Add(collection.ToTextInput().Placeholder("Collection Name"))
            .Add(Layout.Horizontal().Align(Align.Right).Gap(10).Padding(10, 0, 0, 0)
                .Add(new Button("Cancel", onClose))
                .Add(new Button("Create", async () =>
                {
                  if (string.IsNullOrEmpty(name.Value)) return;

                  await service.CreateAssetAsync(new Asset
                  {
                    Name = name.Value,
                    Category = category.Value,
                    Type = type.Value,
                    Collection = collection.Value,
                    AmountGenerated = 0 // Calculated dynamically later
                  });
                  onClose();
                }).Variant(ButtonVariant.Primary))
            )
        ),
        new DialogFooter()
    );
  }
}
