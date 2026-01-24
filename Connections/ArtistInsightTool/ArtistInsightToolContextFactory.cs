using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading;
using Ivy.Services;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ArtistInsightTool.Connections.ArtistInsightTool;

public sealed class ArtistInsightToolContextFactory : IDbContextFactory<ArtistInsightToolContext>
{
  private static readonly SemaphoreSlim InitLock = new(1, 1);

  private readonly ServerArgs _args;
  private readonly string _absolutePath;
  private readonly string _relativePath = "db.sqlite";
  private readonly ILogger? _logger;

  public ArtistInsightToolContextFactory(
      ServerArgs args,
      IVolume? volume = null,
      ILogger? logger = null
  )
  {
    _args = args;
    // Use project root DB for consistent access during development
    _absolutePath = "/Users/joshuang/Desktop/Programming/Ivy/artist-insight-tool/db.sqlite";
    _logger = logger;
  }

  public ArtistInsightToolContext CreateDbContext()
  {
    EnsureDatabasePresentOnce();

    var optionsBuilder = new DbContextOptionsBuilder<ArtistInsightToolContext>()
        .UseSqlite($@"Data Source=""{_absolutePath}""");

    if (_args.Verbose)
    {
      optionsBuilder
          .EnableSensitiveDataLogging()
          .LogTo(s => _logger?.LogInformation("{EFLog}", s), LogLevel.Information);
    }

    var context = new ArtistInsightToolContext(optionsBuilder.Options);

    // Auto-migration for JsonData and ColumnMapping
    // Auto-migration for JsonData and ColumnMapping
    if (!_migrationChecked)
    {
      _migrationChecked = true; // Set immediately to prevent re-entry loops
      try
      {
        context.Database.Migrate();
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Failed to migrate database.");
      }

      try
      {
        context.Database.ExecuteSqlRaw("ALTER TABLE revenue_entries ADD COLUMN JsonData TEXT");
      }
      catch { }

      try
      {
        context.Database.ExecuteSqlRaw("ALTER TABLE revenue_entries ADD COLUMN ColumnMapping TEXT");
      }
      catch { }

      // Create ImportTemplates table if missing
      try
      {
        context.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""import_templates"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_import_templates"" PRIMARY KEY AUTOINCREMENT,
            ""Name"" TEXT NOT NULL,
            ""HeadersJson"" TEXT NOT NULL,
            ""Category"" TEXT DEFAULT 'Other',
            ""AssetColumn"" TEXT,
            ""AmountColumn"" TEXT,
            ""CreatedAt"" TEXT NOT NULL,
            ""UpdatedAt"" TEXT NOT NULL
          )");
        // Seed Template
        var count = context.Database.ExecuteSqlRaw("INSERT OR IGNORE INTO import_templates (Id, Name, HeadersJson, Category, CreatedAt, UpdatedAt) VALUES (99, 'Example Template', '[\"Date\",\"Description\",\"Amount\"]', 'Other', '2024-01-01', '2024-01-01')");
      }
      catch { }

      try
      {
        context.Database.ExecuteSqlRaw("ALTER TABLE import_templates ADD COLUMN Category TEXT DEFAULT 'Other'");
      }
      catch { }

      try
      {
        context.Database.ExecuteSqlRaw("ALTER TABLE revenue_entries ADD COLUMN ImportTemplateId INTEGER REFERENCES import_templates(Id)");
      }
      catch { }

      // RESTORE ARTIST SCHEMA (Revert Fix)
      try
      {
        context.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""artists"" (""Id"" INTEGER NOT NULL CONSTRAINT ""PK_artists"" PRIMARY KEY AUTOINCREMENT, ""Name"" TEXT NOT NULL, ""CreatedAt"" TEXT NOT NULL, ""UpdatedAt"" TEXT NOT NULL)");

        // Ensure default artist exists
        var artistCount = context.Database.ExecuteSqlRaw("INSERT OR IGNORE INTO artists (Id, Name, CreatedAt, UpdatedAt) VALUES (1, 'Primary Artist', '2024-01-01', '2024-01-01')");
      }
      catch { }

      try
      {
        // Default ArtistId to 1
        context.Database.ExecuteSqlRaw("ALTER TABLE revenue_entries ADD COLUMN ArtistId INTEGER DEFAULT 1 REFERENCES artists(Id)");
      }
      catch { }

      try
      {
        context.Database.ExecuteSqlRaw("ALTER TABLE albums ADD COLUMN ArtistId INTEGER DEFAULT 1 REFERENCES artists(Id)");
      }
      catch { }

      try
      {
        context.Database.ExecuteSqlRaw("ALTER TABLE tracks ADD COLUMN ArtistId INTEGER DEFAULT 1 REFERENCES artists(Id)");
      }
      catch { }

      // Template Expansion
      string[] templateCols = {
        "TransactionDateColumn", "TransactionIdColumn", "SourcePlatformColumn",
        "CategoryColumn", "QuantityColumn", "SkuColumn", "CustomerEmailColumn",
        "IsrcColumn", "UpcColumn", "VenueNameColumn", "EventStatusColumn", "TicketClassColumn"
      };
      foreach (var col in templateCols)
      {
#pragma warning disable EF1002
        try { context.Database.ExecuteSqlRaw($"ALTER TABLE import_templates ADD COLUMN {col} TEXT"); } catch { }
#pragma warning restore EF1002
      }
    }

    return context;
  }

  private static bool _migrationChecked = false;

  private void EnsureDatabasePresentOnce()
  {
    if (System.IO.File.Exists(_absolutePath)) return;

    InitLock.Wait();
    try
    {
      if (System.IO.File.Exists(_absolutePath)) return;

      System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_absolutePath)!);

      var dbPath = System.IO.Path.Join(System.AppContext.BaseDirectory, "db.sqlite");
      var templatePath = System.IO.Path.Combine(System.AppContext.BaseDirectory, _relativePath);

      if (!System.IO.File.Exists(templatePath))
      {
        throw new FileNotFoundException(
            $"Database template not found at '{templatePath}'. Make sure '{_relativePath}' is copied to the output folder.");
      }

      var tmp = _absolutePath + ".tmp";
      System.IO.File.Copy(templatePath, tmp, overwrite: true);
      System.IO.File.Move(tmp, _absolutePath);
      _logger?.LogInformation("Initialized persistent database at '{Path}'.", _absolutePath);
    }
    catch (IOException)
    {
      if (!System.IO.File.Exists(_absolutePath)) throw;
    }
    finally
    {
      InitLock.Release();
    }
  }
}
