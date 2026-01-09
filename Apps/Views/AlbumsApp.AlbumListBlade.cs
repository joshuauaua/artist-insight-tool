namespace ArtistInsightTool.Apps.Views;

public class AlbumListBlade : ViewBase
{
    private record AlbumListRecord(int Id, string Title, string? ArtistName);

    public override object? Build()
    {
        var blades = UseContext<IBladeController>();
        var factory = UseService<ArtistInsightToolContextFactory>();
        var refreshToken = this.UseRefreshToken();

        UseEffect(() =>
        {
            if (refreshToken.ReturnValue is int albumId)
            {
                blades.Pop(this, true);
                blades.Push(this, new AlbumDetailsBlade(albumId));
            }
        }, [refreshToken]);

        var onItemClicked = new Action<Event<ListItem>>(e =>
        {
            var album = (AlbumListRecord)e.Sender.Tag!;
            blades.Push(this, new AlbumDetailsBlade(album.Id), album.Title);
        });

        ListItem CreateItem(AlbumListRecord record) =>
            new(title: record.Title, subtitle: record.ArtistName, onClick: onItemClicked, tag: record);

        var createBtn = Icons.Plus.ToButton(_ =>
        {
            blades.Pop(this);
        }).Ghost().Tooltip("Create Album").ToTrigger((isOpen) => new AlbumCreateDialog(isOpen, refreshToken));

        return new FilteredListView<AlbumListRecord>(
            fetchRecords: (filter) => FetchAlbums(factory, filter),
            createItem: CreateItem,
            toolButtons: createBtn,
            onFilterChanged: _ =>
            {
                blades.Pop(this);
            }
        );
    }

    private async Task<AlbumListRecord[]> FetchAlbums(ArtistInsightToolContextFactory factory, string filter)
    {
        await using var db = factory.CreateDbContext();

        var linq = db.Albums.Include(a => a.Artist).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filter = filter.Trim();
            linq = linq.Where(a => a.Title.Contains(filter) || a.Artist.Name.Contains(filter));
        }

        return await linq
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new AlbumListRecord(a.Id, a.Title, a.Artist.Name))
            .ToArrayAsync();
    }
}