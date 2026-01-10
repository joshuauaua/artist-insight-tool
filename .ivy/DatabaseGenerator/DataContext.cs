using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArtistInsightTool;

public class DataContext : DbContext
{
    public DataContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Artist> Artists { get; set; } = null!;
    public DbSet<Album> Albums { get; set; } = null!;
    public DbSet<Track> Tracks { get; set; } = null!;
    public DbSet<Campaign> Campaigns { get; set; } = null!;
    public DbSet<RevenueEntry> RevenueEntries { get; set; } = null!;
    public DbSet<RevenueSource> RevenueSources { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Artist>(entity =>
        {
            entity.ToTable("artists");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("albums");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.ArtistId).IsRequired();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ReleaseDate).IsRequired(false);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasOne(e => e.Artist)
                .WithMany(a => a.Albums)
                .HasForeignKey(e => e.ArtistId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Track>(entity =>
        {
            entity.ToTable("tracks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.ArtistId).IsRequired();
            entity.Property(e => e.AlbumId).IsRequired(false);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Duration).IsRequired(false);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasOne(e => e.Artist)
                .WithMany(a => a.Tracks)
                .HasForeignKey(e => e.ArtistId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Album)
                .WithMany(a => a.Tracks)
                .HasForeignKey(e => e.AlbumId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.ToTable("campaigns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.ArtistId).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.StartDate).IsRequired(false);
            entity.Property(e => e.EndDate).IsRequired(false);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasOne(e => e.Artist)
                .WithMany(a => a.Campaigns)
                .HasForeignKey(e => e.ArtistId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RevenueSource>(entity =>
        {
            entity.ToTable("revenue_sources");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DescriptionText).IsRequired().HasMaxLength(200);
            entity.HasData(RevenueSource.GetSeedData());
        });

        modelBuilder.Entity<RevenueEntry>(entity =>
        {
            entity.ToTable("revenue_entries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.ArtistId).IsRequired();
            entity.Property(e => e.SourceId).IsRequired();
            entity.Property(e => e.Amount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.RevenueDate).IsRequired();
            entity.Property(e => e.Description).IsRequired(false).HasMaxLength(500);
            entity.Property(e => e.TrackId).IsRequired(false);
            entity.Property(e => e.AlbumId).IsRequired(false);
            entity.Property(e => e.CampaignId).IsRequired(false);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasOne(e => e.Artist)
                .WithMany(a => a.RevenueEntries)
                .HasForeignKey(e => e.ArtistId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Source)
                .WithMany()
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Track)
                .WithMany(t => t.RevenueEntries)
                .HasForeignKey(e => e.TrackId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Album)
                .WithMany(a => a.RevenueEntries)
                .HasForeignKey(e => e.AlbumId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Campaign)
                .WithMany(c => c.RevenueEntries)
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

[Table("artists")]
public class Artist
{
    [Key]
    public int Id { get; set; }
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;
    [Required]
    public DateTime CreatedAt { get; set; }
    [Required]
    public DateTime UpdatedAt { get; set; }
    public ICollection<Album> Albums { get; set; } = null!;
    public ICollection<Track> Tracks { get; set; } = null!;
    public ICollection<Campaign> Campaigns { get; set; } = null!;
    public ICollection<RevenueEntry> RevenueEntries { get; set; } = null!;
}

[Table("albums")]
public class Album
{
    [Key]
    public int Id { get; set; }
    [Required]
    public int ArtistId { get; set; }
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;
    public DateTime? ReleaseDate { get; set; }
    [Required]
    public DateTime CreatedAt { get; set; }
    [Required]
    public DateTime UpdatedAt { get; set; }
    [ForeignKey(nameof(ArtistId))]
    public Artist Artist { get; set; } = null!;
    public ICollection<Track> Tracks { get; set; } = null!;
    public ICollection<RevenueEntry> RevenueEntries { get; set; } = null!;
    [Required]
    [MaxLength(50)]
    public string ReleaseType { get; set; } = "Album";
}

[Table("tracks")]
public class Track
{
    [Key]
    public int Id { get; set; }
    [Required]
    public int ArtistId { get; set; }
    public int? AlbumId { get; set; }
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;
    public int? Duration { get; set; }
    [Required]
    public DateTime CreatedAt { get; set; }
    [Required]
    public DateTime UpdatedAt { get; set; }
    [ForeignKey(nameof(ArtistId))]
    public Artist Artist { get; set; } = null!;
    [ForeignKey(nameof(AlbumId))]
    public Album? Album { get; set; }
    public ICollection<RevenueEntry> RevenueEntries { get; set; } = null!;
}

[Table("campaigns")]
public class Campaign
{
    [Key]
    public int Id { get; set; }
    [Required]
    public int ArtistId { get; set; }
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [Required]
    public DateTime CreatedAt { get; set; }
    [Required]
    public DateTime UpdatedAt { get; set; }
    [ForeignKey(nameof(ArtistId))]
    public Artist Artist { get; set; } = null!;
    public ICollection<RevenueEntry> RevenueEntries { get; set; } = null!;
}

[Table("revenue_entries")]
public class RevenueEntry
{
    [Key]
    public int Id { get; set; }
    [Required]
    public int ArtistId { get; set; }
    [Required]
    public int SourceId { get; set; }
    [Required]
    public decimal Amount { get; set; }
    [Required]
    public DateTime RevenueDate { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    public int? TrackId { get; set; }
    public int? AlbumId { get; set; }
    public int? CampaignId { get; set; }
    [Required]
    public DateTime CreatedAt { get; set; }
    [Required]
    public DateTime UpdatedAt { get; set; }
    [ForeignKey(nameof(ArtistId))]
    public Artist Artist { get; set; } = null!;
    [ForeignKey(nameof(SourceId))]
    public RevenueSource Source { get; set; } = null!;
    [ForeignKey(nameof(TrackId))]
    public Track? Track { get; set; }
    [ForeignKey(nameof(AlbumId))]
    public Album? Album { get; set; }
    [ForeignKey(nameof(CampaignId))]
    public Campaign? Campaign { get; set; }
}

[Table("revenue_sources")]
public class RevenueSource
{
    [Key]
    public int Id { get; set; }
    [Required]
    [MaxLength(200)]
    public string DescriptionText { get; set; } = null!;

    public static RevenueSource[] GetSeedData()
    {
        return new RevenueSource[]
        {
            new RevenueSource { Id = 1, DescriptionText = "Concert" },
            new RevenueSource { Id = 2, DescriptionText = "Sync" },
            new RevenueSource { Id = 3, DescriptionText = "Streams" },
            new RevenueSource { Id = 4, DescriptionText = "Merch" },
            new RevenueSource { Id = 5, DescriptionText = "Other" }
        };
    }
}