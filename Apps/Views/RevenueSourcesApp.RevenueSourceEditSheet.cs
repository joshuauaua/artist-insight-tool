namespace ArtistInsightTool.Apps.Views;

public class RevenueSourceEditSheet(int sourceId, IState<int?> isOpen, RefreshToken refreshToken) : ViewBase
{
  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var source = UseState(() => factory.CreateDbContext().RevenueSources.FirstOrDefault(s => s.Id == sourceId)!);
    var openState = UseState(true);

    UseEffect(() =>
    {
      if (!openState.Value)
      {
        isOpen.Set((int?)null);
      }
    }, [openState]);

    UseEffect(() =>
    {
      using var db = factory.CreateDbContext();
      db.RevenueSources.Update(source.Value);
      db.SaveChanges();
      refreshToken.Refresh();
    }, [source]);

    return source
       .ToForm()
       .Builder(s => s.DescriptionText, b => b.ToTextInput())
       .Remove(s => s.Id, s => s.RevenueEntries)
       .ToSheet(openState, "Edit Revenue Source");
  }
}
