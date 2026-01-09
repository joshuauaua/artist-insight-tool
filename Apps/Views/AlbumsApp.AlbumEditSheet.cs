namespace ArtistInsightTool.Apps.Views;

public class AlbumEditSheet(IState<bool> isOpen, RefreshToken refreshToken, int albumId) : ViewBase
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
            .Builder(e => e.ArtistId, e => e.ToAsyncSelectInput(QueryArtists(factory), LookupArtist(factory), placeholder: "Select Artist"))
            .Remove(e => e.Id, e => e.CreatedAt, e => e.UpdatedAt)
            .ToSheet(isOpen, "Edit Album");
    }

    private static AsyncSelectQueryDelegate<int> QueryArtists(ArtistInsightToolContextFactory factory)
    {
        return async query =>
        {
            await using var db = factory.CreateDbContext();
            return (await db.Artists
                    .Where(e => e.Name.Contains(query))
                    .Select(e => new { e.Id, e.Name })
                    .Take(50)
                    .ToArrayAsync())
                .Select(e => new Option<int>(e.Name, e.Id))
                .ToArray();
        };
    }

    private static AsyncSelectLookupDelegate<int> LookupArtist(ArtistInsightToolContextFactory factory)
    {
        return async id =>
        {
            await using var db = factory.CreateDbContext();
            var artist = await db.Artists.FirstOrDefaultAsync(e => e.Id == id);
            if (artist == null) return null;
            return new Option<int>(artist.Name, artist.Id);
        };
    }
}