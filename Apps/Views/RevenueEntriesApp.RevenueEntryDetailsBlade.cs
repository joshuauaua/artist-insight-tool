namespace ArtistInsightTool.Apps.Views;

public class RevenueEntryDetailsBlade(int revenueEntryId) : ViewBase
{
  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var blades = UseContext<IBladeController>();
    var refreshToken = this.UseRefreshToken();
    var revenueEntry = UseState<RevenueEntry?>(() => null!);
    var (alertView, showAlert) = this.UseAlert();

    UseEffect(async () =>
    {
      var db = factory.CreateDbContext();
      revenueEntry.Set(await db.RevenueEntries
              .Include(e => e.Artist)
              .Include(e => e.Source)
              .Include(e => e.Track)
              .Include(e => e.Album)
              .Include(e => e.Campaign)
              .SingleOrDefaultAsync(e => e.Id == revenueEntryId));
    }, [EffectTrigger.AfterInit(), refreshToken]);

    if (revenueEntry.Value == null) return null;

    var revenueEntryValue = revenueEntry.Value;

    var onDelete = () =>
    {
      showAlert("Are you sure you want to delete this revenue entry?", result =>
          {
          if (result.IsOk())
          {
            Delete(factory);
            blades.Pop(refresh: true);
          }
        }, "Delete Revenue Entry", AlertButtonSet.OkCancel);
    };

    var dropDown = Icons.Ellipsis
        .ToButton()
        .Ghost()
        .WithDropDown(
            MenuItem.Default("Delete").Icon(Icons.Trash).HandleSelect(onDelete)
        );

    var editBtn = new Button("Edit")
        .Variant(ButtonVariant.Outline)
        .Icon(Icons.Pencil)
        .Width(Size.Grow())
        .ToTrigger((isOpen) => new RevenueEntryEditSheet(isOpen, refreshToken, revenueEntryId));

    var detailsCard = new Card(
        content: new
        {
          Id = revenueEntryValue.Id,
          Artist = revenueEntryValue.Artist.Name,
          Source = revenueEntryValue.Source.DescriptionText,
          Amount = revenueEntryValue.Amount,
          RevenueDate = revenueEntryValue.RevenueDate,
          Description = revenueEntryValue.Description,
          Track = revenueEntryValue.Track?.Title ?? "N/A",
          Album = revenueEntryValue.Album?.Title ?? "N/A",
          Campaign = revenueEntryValue.Campaign?.Name ?? "N/A"
        }
            .ToDetails()
            .MultiLine(e => e.Description)
            .RemoveEmpty()
            .Builder(e => e.Id, e => e.CopyToClipboard()),
        footer: Layout.Horizontal().Gap(2).Align(Align.Right)
                | dropDown
                | editBtn
    ).Title("Revenue Entry Details").Width(Size.Units(100));

    return new Fragment()
           | (Layout.Vertical() | detailsCard)
           | alertView;
  }

  private void Delete(ArtistInsightToolContextFactory dbFactory)
  {
    using var db = dbFactory.CreateDbContext();
    var revenueEntry = db.RevenueEntries.FirstOrDefault(e => e.Id == revenueEntryId)!;
    db.RevenueEntries.Remove(revenueEntry);
    db.SaveChanges();
  }
}