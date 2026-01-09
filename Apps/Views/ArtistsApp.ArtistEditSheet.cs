namespace ArtistInsightTool.Apps.Views;

public class ArtistEditSheet(IState<bool> isOpen, RefreshToken refreshToken, int artistId) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var artist = UseState(() => factory.CreateDbContext().Artists.FirstOrDefault(e => e.Id == artistId)!);

        UseEffect(() =>
        {
            using var db = factory.CreateDbContext();
            artist.Value.UpdatedAt = DateTime.UtcNow;
            db.Artists.Update(artist.Value);
            db.SaveChanges();
            refreshToken.Refresh();
        }, [artist]);

        return artist
            .ToForm()
            .Builder(e => e.Name, e => e.ToTextInput())
            .Remove(e => e.Id, e => e.CreatedAt, e => e.UpdatedAt)
            .ToSheet(isOpen, "Edit Artist");
    }
}