namespace ArtistInsightTool.Apps.Views;

public class TrackRevenueEntriesBlade(int? trackId) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var refreshToken = this.UseRefreshToken();
        var revenueEntries = this.UseState<RevenueEntry[]?>();
        var (alertView, showAlert) = this.UseAlert();

        this.UseEffect(async () =>
        {
            if (trackId == null) return;
            await using var db = factory.CreateDbContext();
            revenueEntries.Set(await db.RevenueEntries
                .Include(e => e.Source)
                .Where(e => e.TrackId == trackId)
                .ToArrayAsync());
        }, [ EffectTrigger.AfterInit(), refreshToken ]);

        Action OnDelete(int id)
        {
            return () =>
            {
                showAlert("Are you sure you want to delete this revenue entry?", result =>
                {
                    if (result.IsOk())
                    {
                        Delete(factory, id);
                        refreshToken.Refresh();
                    }
                }, "Delete Revenue Entry", AlertButtonSet.OkCancel);
            };
        }

        if (revenueEntries.Value == null) return null;

        var table = revenueEntries.Value.Select(e => new
            {
                Source = e.Source.DescriptionText,
                Amount = e.Amount,
                RevenueDate = e.RevenueDate,
                Description = e.Description,
                _ = Layout.Horizontal().Gap(2)
                    | Icons.Ellipsis
                        .ToButton()
                        .Ghost()
                        .WithDropDown(MenuItem.Default("Delete").Icon(Icons.Trash).HandleSelect(OnDelete(e.Id)))
                    | Icons.Pencil
                        .ToButton()
                        .Outline()
                        .Tooltip("Edit")
                        .ToTrigger((isOpen) => new TrackRevenueEntriesEditSheet(isOpen, refreshToken, e.Id))
            })
            .ToTable()
            .RemoveEmptyColumns();

        var addBtn = new Button("Add Revenue Entry").Icon(Icons.Plus).Outline()
            .ToTrigger((isOpen) => new TrackRevenueEntriesCreateDialog(isOpen, refreshToken, trackId));

        return new Fragment()
               | BladeHelper.WithHeader(addBtn, table)
               | alertView;
    }

    public void Delete(ArtistInsightToolContextFactory factory, int revenueEntryId)
    {
        using var db = factory.CreateDbContext();
        db.RevenueEntries.Remove(db.RevenueEntries.Single(e => e.Id == revenueEntryId));
        db.SaveChanges();
    }
}