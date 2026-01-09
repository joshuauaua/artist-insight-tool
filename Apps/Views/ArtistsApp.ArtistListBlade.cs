namespace ArtistInsightTool.Apps.Views;

public class ArtistListBlade : ViewBase
{
    private record ArtistListRecord(int Id, string Name);

    public override object? Build()
    {
        var blades = UseContext<IBladeController>();
        var factory = UseService<ArtistInsightToolContextFactory>();
        var refreshToken = this.UseRefreshToken();

        UseEffect(() =>
        {
            if (refreshToken.ReturnValue is int artistId)
            {
                blades.Pop(this, true);
                blades.Push(this, new ArtistDetailsBlade(artistId));
            }
        }, [refreshToken]);

        var onItemClicked = new Action<Event<ListItem>>(e =>
        {
            var artist = (ArtistListRecord)e.Sender.Tag!;
            blades.Push(this, new ArtistDetailsBlade(artist.Id), artist.Name);
        });

        ListItem CreateItem(ArtistListRecord record) =>
            new(title: record.Name, subtitle: null, onClick: onItemClicked, tag: record);

        var createBtn = Icons.Plus.ToButton(_ =>
        {
            blades.Pop(this);
        }).Ghost().Tooltip("Create Artist").ToTrigger((isOpen) => new ArtistCreateDialog(isOpen, refreshToken));

        return new FilteredListView<ArtistListRecord>(
            fetchRecords: (filter) => FetchArtists(factory, filter),
            createItem: CreateItem,
            toolButtons: createBtn,
            onFilterChanged: _ =>
            {
                blades.Pop(this);
            }
        );
    }

    private async Task<ArtistListRecord[]> FetchArtists(ArtistInsightToolContextFactory factory, string filter)
    {
        await using var db = factory.CreateDbContext();

        var linq = db.Artists.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filter = filter.Trim();
            linq = linq.Where(e => e.Name.Contains(filter));
        }

        return await linq
            .OrderByDescending(e => e.CreatedAt)
            .Take(50)
            .Select(e => new ArtistListRecord(e.Id, e.Name))
            .ToArrayAsync();
    }
}