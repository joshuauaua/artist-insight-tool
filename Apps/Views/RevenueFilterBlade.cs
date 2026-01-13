using Ivy.Shared; // For Layout, Button, etc.

namespace ArtistInsightTool.Apps.Views;

public class RevenueFilterBlade(
    string currentSortField,
    string currentSortDirection,
    string? currentFilterSource,
    Action<string, string, string?> onApply
) : ViewBase
{
  private readonly string _sortField = currentSortField;
  private readonly string _sortDirection = currentSortDirection;
  private readonly string? _filterSource = currentFilterSource;
  private readonly Action<string, string, string?> _onApply = onApply;

  public string Title => "Filter & Sort";

  public override object? Build()
  {
    var sortField = UseState(_sortField);
    var sortDirection = UseState(_sortDirection);
    var filterSource = UseState<string?>(() => _filterSource);

    // Helper to create a selectable button
    Func<string, string, Action, object> ToggleBtn = (text, activeValue, onClick) =>
    {
      var isActive = text == activeValue || (activeValue == "All" && text == "All" && filterSource.Value == null);
      // Since we don't have explicit styles, we might append a checkmark or similar
      var label = isActive ? $"✓ {text}" : text;
      return new Button(label, onClick);
    };

    // Section Helper
    Func<string, object, object> Section = (title, content) =>
        Layout.Vertical().Gap(5)
            .Add(title) // Simple string for title
            .Add(content);

    return Layout.Vertical()
        .Gap(20)
        .Padding(20)
        .Add(Section("Sort By", Layout.Vertical().Gap(5)
            .Add(ToggleBtn("Date", sortField.Value, () => sortField.Set("Date")))
            .Add(ToggleBtn("Amount", sortField.Value, () => sortField.Set("Amount")))
            .Add(ToggleBtn("Name", sortField.Value, () => sortField.Set("Name")))
            .Add(ToggleBtn("Type", sortField.Value, () => sortField.Set("Type")))
            .Add(ToggleBtn("Campaign", sortField.Value, () => sortField.Set("Campaign")))
        ))
        .Add(Section("Sort Order", Layout.Horizontal().Gap(10)
            .Add(ToggleBtn("Asc", sortDirection.Value, () => sortDirection.Set("Asc")))
            .Add(ToggleBtn("Desc", sortDirection.Value, () => sortDirection.Set("Desc")))
        ))
        .Add(Section("Filter Source", Layout.Vertical().Gap(5)
            .Add(ToggleBtn("All", filterSource.Value ?? "All", () => filterSource.Set((string?)null)))
            .Add(ToggleBtn("Streams", filterSource.Value ?? "All", () => filterSource.Set("Streams"))) // Mapping "Streams" to exact string if matches DB
            .Add(ToggleBtn("Merch", filterSource.Value ?? "All", () => filterSource.Set("Merch")))
        ))
        .Add(new Button("Apply filters", () =>
        {
          _onApply(sortField.Value, sortDirection.Value, filterSource.Value);
        }));
  }
}
