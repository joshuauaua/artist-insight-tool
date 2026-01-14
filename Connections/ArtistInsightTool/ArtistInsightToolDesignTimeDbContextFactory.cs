using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.IO;

namespace ArtistInsightTool.Connections.ArtistInsightTool;

public class ArtistInsightToolDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ArtistInsightToolContext>
{
  public ArtistInsightToolContext CreateDbContext(string[] args)
  {
    // Verified path from system search
    var absolutePath = "/Users/joshuang/Library/Application Support/Ivy/ArtistInsightTool/23bcf718.db.sqlite";

    var optionsBuilder = new DbContextOptionsBuilder<ArtistInsightToolContext>();
    optionsBuilder.UseSqlite($@"Data Source=""{absolutePath}""");

    return new ArtistInsightToolContext(optionsBuilder.Options);
  }
}
