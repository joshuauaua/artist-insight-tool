namespace ArtistInsightTool.Apps.Views;

public class ArtistRevenueEntriesBlade(int artistId) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var refreshToken = this.UseRefreshToken();
        var revenueEntries = this.UseState<RevenueEntry[]?>();
        var (alertView, showAlert) = this.UseAlert();

        this.UseEffect(async () =>
        {
            await using var db = factory.CreateDbContext();
            revenueEntries.Set(await db.RevenueEntries
                .Include(e => e.Source)
                .Include(e => e.Track)
                .Include(e => e.Album)
                .Include(e => e.Campaign)
                .Where(e => e.ArtistId == artistId)
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
                Track = e.Track?.Title ?? "N/A",
                Album = e.Album?.Title ?? "N/A",
                Campaign = e.Campaign?.Name ?? "N/A",
                Description = e.Description ?? "N/A",
                _ = Layout.Horizontal().Gap(2)
                    | Icons.Ellipsis
                        .ToButton()
                        .Ghost()
                        .WithDropDown(MenuItem.Default("Delete").Icon(Icons.Trash).HandleSelect(OnDelete(e.Id)))
                    | Icons.ChevronRight
                        .ToButton()
                        .Outline()
                        .Tooltip("Edit")
                        .ToTrigger((isOpen) => new ArtistRevenueEntriesEditSheet(isOpen, refreshToken, e.Id))
            })
            .ToTable()
            .RemoveEmptyColumns();

        var addBtn = new Button("Add Revenue Entry").Icon(Icons.Plus).Outline()
            .ToTrigger((isOpen) => new ArtistRevenueEntriesCreateDialog(isOpen, refreshToken, artistId));

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