using System.Globalization;
using ArtistInsightTool.Connections.ArtistInsightTool;
using Ivy.Shared;
using Microsoft.EntityFrameworkCore;

namespace ArtistInsightTool.Apps.Views;

public class RevenueCreateSheet(Action onClose) : ViewBase
{
  private readonly Action _onClose = onClose;

  public override object? Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();
    var client = UseService<IClientProvider>();

    // Form States
    var amountState = UseState("");
    var descriptionState = UseState(""); // Maps to "Name"
    var sourceState = UseState("Manual"); // Maps to "Source/Integration"
    var dateState = UseState(DateTime.Now);
    var dateStringState = UseState(DateTime.Now.ToString("MM/dd/yyyy"));

    var revenueSources = UseState<List<RevenueSource>>([]);
    var selectedTypeId = UseState<int?>(() => null);

    // Load Revenue Sources
    UseEffect(async () =>
    {
      await using var db = factory.CreateDbContext();
      var sources = await db.RevenueSources.OrderBy(s => s.DescriptionText).ToListAsync();
      revenueSources.Set(sources);

      // Default to "Other" or first available if possible, or just null
      // Let's try to find "Streams" or something common as default if we want
      var defaultSource = sources.FirstOrDefault(s => s.DescriptionText == "Streams") ?? sources.FirstOrDefault();
      if (defaultSource != null) selectedTypeId.Set(defaultSource.Id);

    }, []);

    return Layout.Vertical()
        .Gap(20)
        .Padding(20)
        .Add(Layout.Horizontal().Align(Align.Center)
            .Add(new Button("← Back", _onClose).Variant(ButtonVariant.Primary))
        )
        .Add(new Card(
            Layout.Vertical().Gap(15)
                .Add(Layout.Vertical().Gap(5)
                    .Add("Entry Details")
                    .Add(Layout.Vertical().Gap(5)
                        .Add("Name")
                        .Add(descriptionState.ToTextInput().Placeholder("e.g. Monthly Payout"))
                    )
                    .Add(Layout.Vertical().Gap(5)
                         .Add("Type")
                         .Add(selectedTypeId.ToSelectInput(
                             revenueSources.Value.Select(s => new Option<int?>(s.DescriptionText, s.Id)).ToList()
                         ).Placeholder("Select Type"))
                    )
                     .Add(Layout.Vertical().Gap(5)
                         .Add("Source")
                         .Add(sourceState.ToTextInput().Placeholder("e.g. Spotify, MerchTable"))
                    )
                )
                .Add(Layout.Vertical().Gap(15)
                    .Add(Layout.Vertical().Gap(5)
                        .Add("Amount ($)")
                        .Add(amountState.ToTextInput().Placeholder("0.00"))
                    )
                     .Add(Layout.Vertical().Gap(5)
                        .Add("Date (MM/dd/yyyy)")
                        .Add(dateStringState.ToTextInput())
                    )
                )
        ).Title("Create New Entry"))
        .Add(Layout.Horizontal().Width(Size.Full()).Align(Align.Right).Gap(10)
             .Add(new Button("Cancel", _onClose).Variant(ButtonVariant.Outline))
             .Add(new Button("Create Entry", async () =>
             {
               if (string.IsNullOrWhiteSpace(descriptionState.Value))
               {
                 client.Toast("Please enter a Name", "Warning");
                 return;
               }

               if (selectedTypeId.Value == null)
               {
                 client.Toast("Please select a Type", "Warning");
                 return;
               }

               if (decimal.TryParse(amountState.Value, out var newAmount))
               {
                 if (DateTime.TryParse(dateStringState.Value, out var newDate))
                 {
                   await using var db = factory.CreateDbContext();

                   // Link to first artist
                   var artist = await db.Artists.FirstOrDefaultAsync();
                   if (artist == null)
                   {
                     client.Toast("No artist found to link entry to.", "Error");
                     return;
                   }

                   var newEntry = new RevenueEntry
                   {
                     ArtistId = artist.Id, // Required
                     SourceId = selectedTypeId.Value.Value,
                     Integration = sourceState.Value,
                     Description = descriptionState.Value,
                     Amount = newAmount,
                     RevenueDate = newDate,
                     CreatedAt = DateTime.UtcNow,
                     UpdatedAt = DateTime.UtcNow
                   };

                   db.RevenueEntries.Add(newEntry);
                   await db.SaveChangesAsync();

                   client.Toast("Entry created successfully", "Success");
                   _onClose();
                 }
                 else
                 {
                   client.Toast("Invalid Date Format", "Error");
                 }
               }
               else
               {
                 client.Toast("Invalid Amount", "Error");
               }
             }).Variant(ButtonVariant.Primary))
        );
  }
}
