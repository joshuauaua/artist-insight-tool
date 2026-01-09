namespace ArtistInsightTool.Apps.Views;

public class ArtistAlbumsEditSheet(IState<bool> isOpen, RefreshToken refreshToken, int albumId) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var album = UseState(() => factory.CreateDbContext().Albums.FirstOrDefault(e => e.Id == albumId)!);

        UseEffect(() =>
        {
            using var db = factory.CreateDbContext();
            album.Value.UpdatedAt = DateTime.UtcNow;
            db.Albums.Update(album.Value);
            db.SaveChanges();
            refreshToken.Refresh();
        }, [album]);

        return album
            .ToForm()
            .Builder(e => e.Title, e => e.ToTextInput())
            .Builder(e => e.ReleaseDate, e => e.ToDateInput())
            .Remove(e => e.Id, e => e.CreatedAt, e => e.UpdatedAt)
            .ToSheet(isOpen, "Edit Album");
    }
}