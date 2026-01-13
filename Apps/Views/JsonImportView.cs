using Ivy.Shared;
using ArtistInsightTool.Connections.ArtistInsightTool;

namespace ArtistInsightTool.Apps.Views;

public class JsonImportView : ViewBase
{
  public override object? Build()
  {
    var jsonContent = UseState("");
    var logs = UseState<List<string>>([]);
    var factory = UseService<ArtistInsightToolContextFactory>();

    Func<string, Task> Log = async (msg) =>
    {
      logs.Set(l => [.. l, $"{DateTime.Now:HH:mm:ss}: {msg}"]);
    };

    Func<Task> ImportJson = async () =>
    {
      if (string.IsNullOrWhiteSpace(jsonContent.Value))
      {
        await Log("Error: JSON content is empty.");
        return;
      }

      await Log("Processing JSON... (Not implemented yet)");
      // Placeholder for future implementation
      await Task.Delay(500);
      await Log("Done.");
    };

    return Layout.Vertical()
        .Gap(20)
        .Padding(20)
        .Add(new Card(
            Layout.Vertical().Gap(15)
                .Add("JSON Import")
                .Add("Paste your revenue data in JSON format below.")
                .Add(jsonContent.ToTextInput().Placeholder("[{\"date\": \"2025-01-01\", \"amount\": 100.0, ...}]"))
                .Add(new Button("Import Data", async () => await ImportJson()))
        ))
        .Add(new Card(
            Layout.Vertical().Gap(10)
                .Add("Import Log")
                .Add(Layout.Vertical().Gap(5)
                    .Add(logs.Value.Select(l => l.ToString()).ToArray())
                )
        ));
  }
}
