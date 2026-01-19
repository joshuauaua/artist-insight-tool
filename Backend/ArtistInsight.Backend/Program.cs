using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using ArtistInsight.Backend.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ArtistInsightContext>(options =>
{
  // Use project root DB for consistent access during development
  var dbPath = "/Users/joshuang/Desktop/Programming/Ivy/artist-insight-tool/db.sqlite";
  Console.WriteLine($"DEBUG: Configuring DbContext with Path: {dbPath}");
  options.UseSqlite($"Data Source={dbPath}");
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
      options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
