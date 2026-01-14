using Ivy.Shared;
using MiniExcelLibs;
using System.IO;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Microsoft.EntityFrameworkCore;

namespace MiniExcelExample;

// --- Helpers ---

// Helper for cleaning up events
public class EffectDisposable(Action action) : IDisposable
{
  public void Dispose() => action();
}

public static class BladeHelper
{
  public static object WithHeader(object header, object content)
  {
    return Layout.Vertical().Gap(20).Padding(20)
        .Add(header)
        .Add(new Separator())
        .Add(content);
  }
}


// --- Apps ---

[App(icon: Icons.Sheet, title: "MiniExcel - Revenue Edit", path: ["Examples", "MiniExcel - Revenue Edit"])]
public class MiniExcelEditApp : ViewBase
{
  public override object? Build()
  {
    return this.UseBlades(() => new RevenueListBlade(), "Revenue");
  }
}

public class RevenueListBlade : ViewBase
{
  public override object? Build()
  {
    var blades = this.UseContext<IBladeController>();
    var factory = this.UseService<ArtistInsightToolContextFactory>();
    var refreshToken = this.UseRefreshToken();
    var searchTerm = this.UseState("");
    var revenueEntries = this.UseState<List<RevenueEntry>>([]);

    this.UseEffect(async () =>
    {
      await using var db = factory.CreateDbContext();
      var data = await db.RevenueEntries
              .Include(r => r.Artist)
              .Include(r => r.Source)
              .OrderByDescending(r => r.RevenueDate)
              .ToListAsync();

      revenueEntries.Set(data);
      return (IDisposable?)null;
    }, [refreshToken.ToTrigger()]);

    var filteredEntries = string.IsNullOrWhiteSpace(searchTerm.Value)
        ? revenueEntries.Value
        : revenueEntries.Value.Where(s =>
            (s.Description?.Contains(searchTerm.Value, StringComparison.OrdinalIgnoreCase) ?? false) ||
            s.Artist.Name.Contains(searchTerm.Value, StringComparison.OrdinalIgnoreCase) ||
            s.Source.DescriptionText.Contains(searchTerm.Value, StringComparison.OrdinalIgnoreCase)
        ).ToList();

    var addButton = Icons.Plus
        .ToButton()
        .Variant(ButtonVariant.Primary)
        .ToTrigger((isOpen) => new RevenueCreateDialog(isOpen, refreshToken));

    // Use a container for the list content
    var content = Layout.Vertical().Gap(5);

    if (filteredEntries.Count > 0)
    {
      foreach (var r in filteredEntries)
      {
        // Explicitly wrapping in Card, relying on GlobalUsings for Card
        var card = new Card(
            Layout.Vertical().Padding(10)
            .Add(Layout.Horizontal().Gap(5)
                .Add(Text.Markdown($"**{r.Artist.Name}**"))
                .Add(Text.Small("•"))
                .Add(Text.Small(r.RevenueDate.ToShortDateString()))
            )
            .Add(Layout.Horizontal()
                .Add(Text.Small($"{r.Source.DescriptionText}"))
                .Add(new Spacer())
                .Add(Text.Markdown($"**${r.Amount:N2}**"))
            )
        ).HandleClick(e => blades.Push(this, new RevenueDetailBlade(r.Id, () => refreshToken.Refresh()), $"Entry #{r.Id}"));

        content.Add(card);
      }
    }
    else if (revenueEntries.Value.Count > 0)
    {
      content.Add(Layout.Center().Add(Text.Small($"No entries found matching '{searchTerm.Value}'")));
    }
    else
    {
      content.Add(Layout.Center().Add(Text.Small("No revenue entries. Add the first record.")));
    }

    return BladeHelper.WithHeader(
        Layout.Horizontal().Gap(10)
            .Add(searchTerm.ToTextInput().Placeholder("Search revenue...").Width(Size.Grow()))
            .Add(addButton)
        ,
        content
    );
  }
}

public class RevenueDetailBlade(int entryId, Action? onRefresh = null) : ViewBase
{
  public override object? Build()
  {
    var blades = this.UseContext<IBladeController>();
    var factory = this.UseService<ArtistInsightToolContextFactory>();
    var refreshToken = this.UseRefreshToken();
    var (alertView, showAlert) = this.UseAlert();

    var entryState = this.UseState<RevenueEntry?>(() => null);

    async Task LoadEntry()
    {
      await using var db = factory.CreateDbContext();
      var entry = await db.RevenueEntries
         .Include(r => r.Artist)
         .Include(r => r.Source)
         .FirstOrDefaultAsync(r => r.Id == entryId);
      entryState.Set(entry);
    }

    this.UseEffect(async () =>
    {
      await LoadEntry();
      return (IDisposable?)null;
    }, [refreshToken.ToTrigger()]);

    var entry = entryState.Value;
    if (entry == null) return Layout.Center().Add(Text.Small("Loading..."));

    var editButton = new Button("Edit")
        .Icon(Icons.Pencil)
        .Variant(ButtonVariant.Outline)
        .ToTrigger((isOpen) => new RevenueEditSheet(isOpen, entryId, refreshToken, () =>
        {
          refreshToken.Refresh();
          onRefresh?.Invoke();
        }));

    var onDelete = new Action(() =>
    {
      showAlert($"Are you sure you want to delete this {entry.Amount:C} entry?", async result =>
          {
            if (result.IsOk())
            {
              await using var db = factory.CreateDbContext();
              var toDelete = await db.RevenueEntries.FindAsync(entryId);
              if (toDelete != null)
              {
                db.RevenueEntries.Remove(toDelete);
                await db.SaveChangesAsync();
              }

              onRefresh?.Invoke();
              blades.Pop(refresh: true);
            }
          }, "Delete Entry", AlertButtonSet.OkCancel);
    });

    return new Fragment(
        BladeHelper.WithHeader(
            Text.H4($"{entry.Artist.Name} - {entry.Amount:C}")
            ,
            Layout.Vertical().Gap(10)
                .Add(new Card(
                    Layout.Vertical().Gap(10)
                    .Add(new
                    {
                      Date = entry.RevenueDate.ToShortDateString(),
                      Source = entry.Source.DescriptionText,
                      Description = entry.Description,
                      Integration = entry.Integration
                    }.ToDetails())
                    .Add(Layout.Horizontal().Gap(5)
                        .Add(editButton)
                        .Add(new Button("Delete")
                            .Icon(Icons.Trash)
                            .Variant(ButtonVariant.Destructive)
                            .HandleClick(_ => onDelete())
                        )
                    )
                ))
        ),
        alertView
    );
  }
}


public class RevenueCreateDialog(IState<bool> isOpen, RefreshToken refreshToken) : ViewBase
{
  public override object? Build()
  {
    var factory = this.UseService<ArtistInsightToolContextFactory>();
    var artists = this.UseState<List<Artist>>([]);
    var sources = this.UseState<List<RevenueSource>>([]);

    // Load reference data
    this.UseEffect(async () =>
    {
      await using var db = factory.CreateDbContext();
      artists.Set(await db.Artists.OrderBy(a => a.Name).ToListAsync());
      sources.Set(await db.RevenueSources.OrderBy(s => s.DescriptionText).ToListAsync());
      return (IDisposable?)null;
    }, []);

    var amountStr = UseState("0.00");
    var description = UseState("");
    var date = UseState(DateTime.UtcNow);
    var artistId = UseState<int?>(() => null);
    var sourceId = UseState<int?>(() => null);

    var onSave = new Action(async () =>
    {
      if (decimal.TryParse(amountStr.Value, out var amount) && artistId.Value.HasValue && sourceId.Value.HasValue)
      {
        await using var db = factory.CreateDbContext();
        var newEntry = new RevenueEntry
        {
          Amount = amount,
          Description = description.Value,
          RevenueDate = date.Value,
          ArtistId = artistId.Value.Value,
          SourceId = sourceId.Value.Value,
          CreatedAt = DateTime.UtcNow,
          UpdatedAt = DateTime.UtcNow
        };
        db.RevenueEntries.Add(newEntry);
        await db.SaveChangesAsync();

        refreshToken.Refresh();
        isOpen.Set(false);
      }
    });

    return Layout.Vertical().Gap(15).Width(400)
            .Add(Text.H3("Add Revenue"))
            .Add(Layout.Vertical().Gap(5).Add("Artist").Add(artistId.ToSelectInput(artists.Value.Select(a => new Option<int?>(a.Name, a.Id)).ToList())))
            .Add(Layout.Vertical().Gap(5).Add("Source").Add(sourceId.ToSelectInput(sources.Value.Select(s => new Option<int?>(s.DescriptionText, s.Id)).ToList())))
             .Add(Layout.Vertical().Gap(5).Add("Amount").Add(amountStr.ToTextInput()))
            .Add(Layout.Vertical().Gap(5).Add("Date").Add(date.ToDateInput()))
            .Add(Layout.Vertical().Gap(5).Add("Description").Add(description.ToTextInput()))

            .Add(Layout.Horizontal().Gap(10).Align(Align.Right)
                .Add(new Button("Cancel").Variant(ButtonVariant.Outline).HandleClick(_ => isOpen.Set(false)))
                .Add(new Button("Save").Variant(ButtonVariant.Primary).HandleClick(_ => onSave()))
            );
  }
}

public class RevenueEditSheet(IState<bool> isOpen, int entryId, RefreshToken refreshToken, Action onSaveCallback) : ViewBase
{
  public override object? Build()
  {
    var factory = this.UseService<ArtistInsightToolContextFactory>();
    var artists = this.UseState<List<Artist>>([]);
    var sources = this.UseState<List<RevenueSource>>([]);

    var amountStr = UseState("0.00");
    var description = UseState("");
    var date = UseState(DateTime.UtcNow);
    var artistId = UseState<int?>(() => null);
    var sourceId = UseState<int?>(() => null);

    var needsLoad = UseState(true);

    this.UseEffect(async () =>
    {
      await using var db = factory.CreateDbContext();
      artists.Set(await db.Artists.OrderBy(a => a.Name).ToListAsync());
      sources.Set(await db.RevenueSources.OrderBy(s => s.DescriptionText).ToListAsync());

      if (needsLoad.Value)
      {
        var entry = await db.RevenueEntries.FindAsync(entryId);
        if (entry != null)
        {
          amountStr.Set(entry.Amount.ToString("F2"));
          description.Set(entry.Description ?? "");
          date.Set(entry.RevenueDate);
          artistId.Set(entry.ArtistId);
          sourceId.Set(entry.SourceId);
        }
        needsLoad.Set(false);
      }
      return (IDisposable?)null;
    }, []);


    var onSave = new Action(async () =>
    {
      if (decimal.TryParse(amountStr.Value, out var amount) && artistId.Value.HasValue && sourceId.Value.HasValue)
      {
        await using var db = factory.CreateDbContext();
        var entry = await db.RevenueEntries.FindAsync(entryId);
        if (entry != null)
        {
          entry.Amount = amount;
          entry.Description = description.Value;
          entry.RevenueDate = date.Value;
          entry.ArtistId = artistId.Value.Value;
          entry.SourceId = sourceId.Value.Value;
          entry.UpdatedAt = DateTime.UtcNow;

          await db.SaveChangesAsync();
          refreshToken.Refresh();
          onSaveCallback?.Invoke();
          isOpen.Set(false);
        }
      }
    });

    return Layout.Vertical().Gap(15).Padding(20).Width(400)
            .Add(Text.H3("Edit Revenue"))
            .Add(Layout.Vertical().Gap(5).Add("Artist").Add(artistId.ToSelectInput(artists.Value.Select(a => new Option<int?>(a.Name, a.Id)).ToList())))
            .Add(Layout.Vertical().Gap(5).Add("Source").Add(sourceId.ToSelectInput(sources.Value.Select(s => new Option<int?>(s.DescriptionText, s.Id)).ToList())))
             .Add(Layout.Vertical().Gap(5).Add("Amount").Add(amountStr.ToTextInput()))
            .Add(Layout.Vertical().Gap(5).Add("Date").Add(date.ToDateInput()))
            .Add(Layout.Vertical().Gap(5).Add("Description").Add(description.ToTextInput()))
             .Add(Layout.Horizontal().Gap(10)
                .Add(new Button("Save").Variant(ButtonVariant.Primary).HandleClick(_ => onSave()).Width(Size.Full()))
            );
  }
}


[App(icon: Icons.Sheet, title: "MiniExcel - Revenue View", path: ["Examples", "MiniExcel - Revenue View"])]
public class MiniExcelViewApp : ViewBase
{
  public class RevenueExportItem
  {
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Artist { get; set; } = "";
    public string Source { get; set; } = "";
    public string Description { get; set; } = "";
  }

  public override object? Build()
  {
    var factory = this.UseService<ArtistInsightToolContextFactory>();
    var refreshToken = this.UseRefreshToken();

    var revenueEntries = this.UseState<List<RevenueExportItem>>([]);

    this.UseEffect(async () =>
    {
      await using var db = factory.CreateDbContext();
      // Project directly for export friendliness
      var data = await db.RevenueEntries
              .Select(r => new RevenueExportItem
              {
                Id = r.Id,
                Date = r.RevenueDate,
                Amount = r.Amount,
                Artist = r.Artist.Name,
                Source = r.Source.DescriptionText,
                Description = r.Description ?? ""
              })
              .OrderByDescending(r => r.Date)
              .ToListAsync();

      revenueEntries.Set(data);
      return (IDisposable?)null;
    }, [refreshToken.ToTrigger()]);

    return BuildTableViewPage(revenueEntries, refreshToken, factory);
  }

  private object BuildTableViewPage(IState<List<RevenueExportItem>> entries, RefreshToken refreshToken, ArtistInsightToolContextFactory factory)
  {
    var client = UseService<IClientProvider>();
    var uploadState = this.UseState<FileUpload<byte[]>?>();
    var uploadContext = this.UseUpload(MemoryStreamUploadHandler.Create(uploadState))
        .Accept(".xlsx")
        .MaxFileSize(50 * 1024 * 1024);
    var actionMode = this.UseState("Export");

    var downloadUrl = this.UseDownload(
        async () =>
        {
          await using var ms = new MemoryStream();
          MiniExcel.SaveAs(ms, entries.Value);
          return ms.ToArray();
        },
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"revenue-{DateTime.UtcNow:yyyy-MM-dd}.xlsx"
    );

    this.UseEffect(async () =>
    {
      if (uploadState.Value?.Content is byte[] bytes && bytes.Length > 0)
      {
        try
        {
          await using var db = factory.CreateDbContext();
          using var ms = new MemoryStream(bytes);
          var imported = MiniExcel.Query<RevenueExportItem>(ms).ToList();

          var artists = await db.Artists.ToDictionaryAsync(a => a.Name);
          var sources = await db.RevenueSources.ToDictionaryAsync(s => s.DescriptionText);

          int count = 0;
          foreach (var item in imported)
          {
            // Resolve Artist
            if (!artists.ContainsKey(item.Artist))
            {
              var newArtist = new Artist { Name = item.Artist, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
              db.Artists.Add(newArtist); // Add triggers ID gen on save? No, context tracks it.
              artists[item.Artist] = newArtist; // Cache it
            }

            // Resolve Source (Must exist or default?? For now, create if missing to be safe)
            if (!sources.ContainsKey(item.Source))
            {
              // This might be dangerous if random sources appear, but ok for demo
              var newSource = new RevenueSource
              {
                Id = sources.Count > 0 ? sources.Values.Max(s => s.Id) + 1 : 1, // Manual ID gen if not identity (RevenueSource Id is value generated never in previous view)
                DescriptionText = item.Source
              };
              db.RevenueSources.Add(newSource);
              sources[item.Source] = newSource;
            }

            // Upsert Logic
            var existing = await db.RevenueEntries.FindAsync(item.Id);
            if (existing != null)
            {
              existing.Amount = item.Amount;
              existing.RevenueDate = item.Date;
              existing.Description = item.Description;
              existing.Artist = artists[item.Artist];
              existing.Source = sources[item.Source];
              existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
              var newEntry = new RevenueEntry
              {
                Amount = item.Amount,
                RevenueDate = item.Date,
                Description = item.Description,
                Artist = artists[item.Artist],
                Source = sources[item.Source],
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
              };
              db.RevenueEntries.Add(newEntry);
            }
            count++;
          }

          await db.SaveChangesAsync();

          refreshToken.Refresh();
          client.Toast($"Imported {count} entries");
        }
        catch (Exception ex)
        {
          client.Toast($"Import error: {ex.Message}");
        }
        finally
        {
          uploadState.Set((FileUpload<byte[]>?)null);
        }
      }
      return (IDisposable?)null;
    }, [uploadState]);

    object? actionWidget = actionMode.Value == "Export"
        ? (object)new Button("Download Excel File")
            .Icon(Icons.Download)
            .Variant(ButtonVariant.Primary)
            .Url(downloadUrl.Value)
            .Width(Size.Full())
        : (object)uploadState.ToFileInput(uploadContext)
            .Placeholder("Choose File");

    return Layout.Horizontal().Gap(20)
        .Add(new Card(
            Layout.Vertical().Gap(10)
            .Add(Text.H3("Revenue Data"))
            .Add(Text.Small("Upload/Download Excel"))
            .Add(actionMode.ToSelectInput(new List<string> { "Export", "Import" }.Select(s => new Option<string>(s, s)).ToList()))
            .Add(actionWidget)
            .Add(new Spacer().Height(Size.Units(5)))
            .Add(Text.Small($"Total: {entries.Value.Count} records"))
        ).Width(Size.Fraction(0.4f)))
        .Add(new Card(
            Layout.Vertical()
            .Add(Text.H3("Overview"))
            .Add(entries.Value.Count > 0
                ? entries.Value.AsQueryable().ToDataTable()
                    .Hidden(s => s.Id)
                    .Width(Size.Full())
                    .Height(Size.Units(140))
                    // Key to force refresh on count change
                    .Key($"rev-{entries.Value.Count}")
                : Layout.Center()
                    .Add(Text.Small("No data to display"))
            )
        ).Height(Size.Fit().Min(Size.Full())));
  }
}
