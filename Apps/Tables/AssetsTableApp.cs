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

using Ivy.Hooks;

namespace ArtistInsightTool.Apps.Tables;

// [App(icon: Icons.Table, title: "Assets Table", path: ["Tables"])]
public class AssetsTableApp : ViewBase
{
  public override object Build()
  {
    var service = UseService<ArtistInsightService>();
    var factory = UseService<ArtistInsightToolContextFactory>();

    // Hooks
    var assetsQuery = UseQuery("assets", async (ct) => (await service.GetAssetsAsync()).OrderBy(a => a.Id).ToArray());
    var templatesQuery = UseQuery("templates", async (ct) => await service.GetTemplatesAsync());

    var assets = assetsQuery.Value ?? [];

    // Dynamic Header Logic
    var templates = templatesQuery.Value ?? [];
    var assetTemplate = templates.FirstOrDefault(t => t.Name.Equals("Assets Template", StringComparison.OrdinalIgnoreCase));

    // Default Headers
    var collectionHeader = "Collection";

    if (assetTemplate != null)
    {
      var mappings = assetTemplate.GetMappings();
      if (mappings.ContainsValue("Collection"))
      {
        collectionHeader = mappings.FirstOrDefault(x => x.Value == "Collection").Key;
      }
    }

    var showCreate = UseState(false);
    var showSpotifyImport = UseState(false);
    var selectedAssetId = UseState<int?>(() => null);
    var confirmDeleteId = UseState<int?>(() => null);

    if (showCreate.Value)
    {
      return new CreateAssetSheet(() =>
      {
        showCreate.Set(false);
        assetsQuery.Mutator.Revalidate();
      });
    }

    if (showSpotifyImport.Value)
    {
      return new ImportSpotifyAssetSheet(() =>
      {
        showSpotifyImport.Set(false);
        assetsQuery.Mutator.Revalidate();
      });
    }

    // Search State
    var searchQuery = UseState("");

    // Filter Logic
    var filteredAssets = assets.AsEnumerable();
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
    .Add(x => x.Amount)
    .Add(x => x.Actions)
    .Header(x => x.IdButton, "ID")
    .Header(x => x.Name, "Asset Name")
    .Header(x => x.Category, "Category")
    .Header(x => x.Type, "Type")
    .Header(x => x.Amount, "Amount Generated")
    .Header(x => x.Actions, "");

    var headerCard = new Card(
        Layout.Vertical().Gap(10)
            .Add(Layout.Horizontal().Align(Align.Center).Width(Size.Full())
                 .Add(new Spacer().Width(Size.Fraction(1)))
            )
            .Add(Layout.Horizontal().Width(Size.Full()).Gap(10)
                 .Add(searchQuery.ToTextInput().Placeholder("Search assets...").Width(300))
            )
    );

    var content = Layout.Vertical().Height(Size.Full()).Padding(20, 0, 20, 50)
        .Add(filteredAssets.Any()
            ? table
            : Layout.Center().Add(Text.Label("There is no information to display")));

    return new Fragment(
        new HeaderLayout(headerCard, content),
        selectedAssetId.Value != null ? new Dialog(
            _ => selectedAssetId.Set((int?)null),
            new DialogHeader("Asset Details"),
            new DialogBody(new AssetViewSheet(selectedAssetId.Value.Value, () =>
            {
              selectedAssetId.Set((int?)null);
              assetsQuery.Mutator.Revalidate();
            })),
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
                    assetsQuery.Mutator.Revalidate();
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

    var content = Layout.Vertical().Gap(10).Height(Size.Full())
        .Add(Text.Label("Import Type"))
        .Add(importType.ToSelectInput([new Option<string>("Artist", "Artist"), new Option<string>("Song", "Song")]))
        .Add(Text.Label("Spotify ID"))
        .Add(id.ToTextInput().Placeholder("e.g. 0TnOYISbd1XYRBk9myaseg"))
        .Add(Text.Label("Bearer Token"))
        .Add(token.ToTextInput().Placeholder("Authorization: Bearer ..."))
        .Add(new Spacer())
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
        );

    return new Sheet(
        _ => onClose(),
        content,
        "Fetch Asset from Spotify"
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
      "Concerts" => new[] { "Ticket Sales", "VIP Package", "Merch Bundle", "Meet & Greet" },
      "Merchandise" => new[] { "Single Item", "Merchandise Collection" },
      "Royalties" => new[] { "Single", "EP", "Album" },
      "Other" => new[] { "Grant", "Sponsorship", "Investment", "Licensing" },
      _ => Array.Empty<string>()
    };

    var content = Layout.Vertical().Gap(10).Height(Size.Full())
        .Add(Text.Label("Name"))
        .Add(name.ToTextInput().Placeholder("Asset Name"))
        .Add(Text.Label("Category"))
        .Add(new SelectInput<string>(
            value: category.Value,
            onChange: e =>
            {
              category.Set(e.Value);
              // Reset Type based on new Category
              if (e.Value == "Concerts") type.Set("Ticket Sales");
              else if (e.Value == "Merchandise") type.Set("Single Item");
              else if (e.Value == "Royalties") type.Set("Single");
              else if (e.Value == "Other") type.Set("Grant");
              else type.Set("");
            },
            new[] { "Concerts", "Merchandise", "Royalties", "Other" }.Select(c => new Option<string>(c, c))))
        .Add(Text.Label("Collection Type"))
        .Add(typeOptions.Length == 0
            ? type.ToTextInput().Placeholder("Collection Type")
            : new SelectInput<string>(
                value: type.Value,
                onChange: e => type.Set(e.Value),
                typeOptions.Select(t => new Option<string>(t, t))
              ))
        .Add(Text.Label("Collection"))
        .Add(collection.ToTextInput().Placeholder("Collection Name"))
        .Add(new Spacer())
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
        );

    return new Sheet(
        _ => onClose(),
        content,
        "Create Asset"
    );
  }
}
