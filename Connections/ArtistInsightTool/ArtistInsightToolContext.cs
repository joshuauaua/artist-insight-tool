using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ArtistInsightTool.Connections.ArtistInsightTool;

public partial class ArtistInsightToolContext : DbContext
{
  public ArtistInsightToolContext(DbContextOptions<ArtistInsightToolContext> options)
      : base(options)
  {
  }

  public virtual DbSet<Album> Albums { get; set; }

  public virtual DbSet<Artist> Artists { get; set; }

  public virtual DbSet<Asset> Assets { get; set; }



  public virtual DbSet<EfmigrationsLock> EfmigrationsLocks { get; set; }

  public virtual DbSet<RevenueEntry> RevenueEntries { get; set; }

  public virtual DbSet<RevenueSource> RevenueSources { get; set; }

  public virtual DbSet<Track> Tracks { get; set; }

  public virtual DbSet<ImportTemplate> ImportTemplates { get; set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<Album>(entity =>
    {
      entity.HasOne(d => d.Artist).WithMany(p => p.Albums).OnDelete(DeleteBehavior.Restrict);
    });



    modelBuilder.Entity<EfmigrationsLock>(entity =>
    {
      entity.Property(e => e.Id).ValueGeneratedNever();
    });

    modelBuilder.Entity<RevenueEntry>(entity =>
    {
      entity.HasOne(d => d.Album).WithMany(p => p.RevenueEntries).OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(d => d.Artist).WithMany(p => p.RevenueEntries).OnDelete(DeleteBehavior.Restrict);



      entity.HasOne(d => d.Source).WithMany(p => p.RevenueEntries).OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(d => d.Track).WithMany(p => p.RevenueEntries).OnDelete(DeleteBehavior.Restrict);
    });

    modelBuilder.Entity<RevenueSource>(entity =>
    {
      entity.Property(e => e.Id).ValueGeneratedNever();
    });

    modelBuilder.Entity<Track>(entity =>
    {
      entity.HasOne(d => d.Album).WithMany(p => p.Tracks).OnDelete(DeleteBehavior.Restrict);

      entity.HasOne(d => d.Artist).WithMany(p => p.Tracks).OnDelete(DeleteBehavior.Restrict);
    });

    OnModelCreatingPartial(modelBuilder);
  }

  partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
