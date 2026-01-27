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

      // Default to "Other" or first available
      var defaultSource = sources.FirstOrDefault(s => s.DescriptionText == "Streams") ?? sources.FirstOrDefault();
      if (defaultSource != null) selectedTypeId.Set(defaultSource.Id);

    }, []);

    var typeOptions = revenueSources.Value.Select(s =>
    {
      var label = s.DescriptionText switch
      {
        "Live Show" => "Concert",
        "Merch" => "Merchandise",
        "Streams" => "Royalties (Streams)",
        "Sync" => "Royalties (Sync)",
        "Others" => "Other",
        _ => s.DescriptionText
      };
      return new Option<int?>(label, s.Id);
    }).OrderBy(o => o.Label).ToList();

    var content = Layout.Vertical().Gap(15)
        .Add(Layout.Vertical().Gap(5)
            .Add("Name")
            .Add(descriptionState.ToTextInput().Placeholder("e.g. Monthly Payout"))
        )
        .Add(Layout.Vertical().Gap(5)
             .Add("Type")
             .Add(selectedTypeId.ToSelectInput(typeOptions).Placeholder("Select Type"))
        )
        .Add(Layout.Vertical().Gap(5)
             .Add("Source")
             .Add(sourceState.ToTextInput().Placeholder("e.g. Spotify, MerchTable"))
        )
        .Add(Layout.Vertical().Gap(5)
            .Add("Amount (kr)")
            .Add(amountState.ToTextInput().Placeholder("0.00"))
        )
        .Add(Layout.Vertical().Gap(5)
            .Add("Date (MM/dd/yyyy)")
            .Add(dateStringState.ToTextInput())
        )
        .Add(new Spacer())
        .Add(Layout.Horizontal().Gap(10).Align(Align.Right).Width(Size.Full())
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

    return new Sheet(
        _ => _onClose(),
        content,
        "Create New Entry"
    );
  }
}
