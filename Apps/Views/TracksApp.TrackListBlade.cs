namespace ArtistInsightTool.Apps.Views;

public class TrackListBlade : ViewBase
{
    private record TrackListRecord(int Id, string Title, string? ArtistName);

    public override object? Build()
    {
        var blades = UseContext<IBladeController>();
        var factory = UseService<ArtistInsightToolContextFactory>();
        var refreshToken = this.UseRefreshToken();

        UseEffect(() =>
        {
            if (refreshToken.ReturnValue is int trackId)
            {
                blades.Pop(this, true);
                blades.Push(this, new TrackDetailsBlade(trackId));
            }
        }, [refreshToken]);

        var onItemClicked = new Action<Event<ListItem>>(e =>
        {
            var track = (TrackListRecord)e.Sender.Tag!;
            blades.Push(this, new TrackDetailsBlade(track.Id), track.Title);
        });

        ListItem CreateItem(TrackListRecord record) =>
            new(title: record.Title, subtitle: record.ArtistName, onClick: onItemClicked, tag: record);

        var createBtn = Icons.Plus.ToButton(_ =>
        {
            blades.Pop(this);
        }).Ghost().Tooltip("Create Track").ToTrigger((isOpen) => new TrackCreateDialog(isOpen, refreshToken));

        return new FilteredListView<TrackListRecord>(
            fetchRecords: (filter) => FetchTracks(factory, filter),
            createItem: CreateItem,
            toolButtons: createBtn,
            onFilterChanged: _ =>
            {
                blades.Pop(this);
            }
        );
    }

    private async Task<TrackListRecord[]> FetchTracks(ArtistInsightToolContextFactory factory, string filter)
    {
        await using var db = factory.CreateDbContext();

        var linq = db.Tracks.Include(t => t.Artist).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filter = filter.Trim();
            linq = linq.Where(t => t.Title.Contains(filter) || t.Artist.Name.Contains(filter));
        }

        return await linq
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new TrackListRecord(t.Id, t.Title, t.Artist.Name))
            .ToArrayAsync();
    }
}