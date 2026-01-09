namespace ArtistInsightTool.Apps.Views;

public class ArtistCreateDialog(IState<bool> isOpen, RefreshToken refreshToken) : ViewBase
{
    private record ArtistCreateRequest
    {
        [Required]
        public string Name { get; init; } = "";
    }

    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();
        var artist = UseState(() => new ArtistCreateRequest());

        UseEffect(() =>
        {
            var artistId = CreateArtist(factory, artist.Value);
            refreshToken.Refresh(artistId);
        }, [artist]);

        return artist
            .ToForm()
            .ToDialog(isOpen, title: "Create Artist", submitTitle: "Create");
    }

    private int CreateArtist(ArtistInsightToolContextFactory factory, ArtistCreateRequest request)
    {
        using var db = factory.CreateDbContext();

        var artist = new Artist
        {
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Artists.Add(artist);
        db.SaveChanges();

        return artist.Id;
    }
}