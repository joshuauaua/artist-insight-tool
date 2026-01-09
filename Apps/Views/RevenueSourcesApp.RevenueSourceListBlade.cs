namespace ArtistInsightTool.Apps.Views;

public class RevenueSourceListBlade : ViewBase
{
  private record RevenueSourceRecord(int Id, string DescriptionText);

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var refreshToken = this.UseRefreshToken();
    var sourceToEdit = this.UseState<int?>((int?)null);

    var sources = this.UseState<RevenueSourceRecord[]?>((RevenueSourceRecord[]?)null);

    this.UseEffect(async () =>
    {
      using var db = factory.CreateDbContext();
      var data = await db.RevenueSources
                  .Select(s => new RevenueSourceRecord(s.Id, s.DescriptionText))
                  .ToArrayAsync();
      sources.Set(data);
      return (IDisposable?)null;
    }, [refreshToken]);

    var table = sources.Value?
        .Select(s => new
        {
          s.Id,
          Name = s.DescriptionText,
          Actions = Icons.Pencil
                        .ToButton(_ => sourceToEdit.Set(s.Id))
                        .Outline()
                        .Tooltip("Edit")
        })
        .ToTable()
        .Empty("No revenue sources found");

    return Layout.Vertical()
           | (Layout.Horizontal().Align(Align.Right) | new Button("Create Source").Primary().ToTrigger(open => new RevenueSourceCreateDialog(open, refreshToken))).Padding(10)
           | table
           | (sourceToEdit.Value.HasValue ? new RevenueSourceEditSheet(sourceToEdit.Value.Value, sourceToEdit, refreshToken) : null);
  }
}
