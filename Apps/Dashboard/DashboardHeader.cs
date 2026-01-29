using Ivy.Shared;

namespace ArtistInsightTool.Apps.Dashboard;

public class DashboardHeader(IState<int> selectedTab, Action onImportClick) : ViewBase
{
  public override object Build()
  {
    return Layout.Horizontal().Align(Align.Center).Width(Size.Full()).Gap(10)
        .Padding(10, 6)
        .Add(Text.H3("Artist Ledger").NoWrap())
        .Add(Layout.Horizontal().Gap(1)
            .Add(RenderTabButton("Overview", 0))
            .Add(RenderTabButton("Assets", 1))
            .Add(RenderTabButton("Revenue", 2))
            .Add(RenderTabButton("Uploads", 3))
            .Add(RenderTabButton("Templates", 4))
        )
        .Add(new Spacer())
        .Add(new Button("Import Data", onImportClick)
            .Icon(Icons.FileUp)
            .Variant(ButtonVariant.Primary));
  }

  private object RenderTabButton(string label, int index)
  {
    var isActive = selectedTab.Value == index;
    return new Button(label, () => selectedTab.Set(index))
        .Variant(isActive ? ButtonVariant.Secondary : ButtonVariant.Ghost);
  }
}
