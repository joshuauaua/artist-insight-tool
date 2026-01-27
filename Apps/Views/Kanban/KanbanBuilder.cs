using System.Linq.Expressions;
using Ivy.Core;
using Ivy.Core.Hooks;
using Ivy.Shared;
using Ivy.Views.Builders;

namespace ArtistInsightTool.Apps.Views.Kanban;

public class KanbanBuilder<TModel, TGroupKey>(
    IEnumerable<TModel> records,
    Expression<Func<TModel, TGroupKey>> groupBySelector,
    Expression<Func<TModel, object?>>? cardIdSelector = null,
    Expression<Func<TModel, object?>>? cardOrderSelector = null)
    : ViewBase, IStateless
    where TGroupKey : notnull
{
  private readonly BuilderFactory<TModel> _builderFactory = new();
  private IBuilder<TModel>? _cardBuilder;
  private Expression<Func<TModel, object?>>? _columnOrderBySelector;
  private bool _columnOrderDescending;
  private Expression<Func<TModel, object?>>? _cardOrderBySelector;
  private bool _cardOrderDescending;
  private Func<TModel, object>? _customCardRenderer;
  private Func<Event<Ivy.Kanban, (object? CardId, TGroupKey ToColumn, int? TargetIndex)>, ValueTask>? _onMove;
  private object? _empty;
  private Size? _width = Size.Full();
  private Size? _height = Size.Full();
  private Size? _columnWidth;
  private bool _showCounts = true;
  private int _gap = 10;

  public KanbanBuilder<TModel, TGroupKey> Builder(Func<IBuilderFactory<TModel>, IBuilder<TModel>> builder)
  {
    _cardBuilder = builder(_builderFactory);
    return this;
  }

  public KanbanBuilder<TModel, TGroupKey> CardBuilder(Func<TModel, object> cardRenderer)
  {
    _customCardRenderer = cardRenderer;
    return this;
  }

  public KanbanBuilder<TModel, TGroupKey> ColumnOrder<TOrderKey>(Expression<Func<TModel, TOrderKey>> orderBySelector, bool descending = false)
  {
    _columnOrderBySelector = Expression.Lambda<Func<TModel, object?>>(
        Expression.Convert(orderBySelector.Body, typeof(object)),
        orderBySelector.Parameters);

    _columnOrderDescending = descending;
    return this;
  }

  public KanbanBuilder<TModel, TGroupKey> HideCounts()
  {
    _showCounts = false;
    return this;
  }

  public KanbanBuilder<TModel, TGroupKey> Gap(int gap)
  {
    _gap = gap;
    return this;
  }

  public KanbanBuilder<TModel, TGroupKey> HandleMove(Action<(object? CardId, TGroupKey ToColumn, int? TargetIndex)> onMove)
  {
    _onMove = e => { onMove(e.Value); return ValueTask.CompletedTask; };
    return this;
  }

  public KanbanBuilder<TModel, TGroupKey> Width(Size? width)
  {
    _width = width;
    return this;
  }

  public KanbanBuilder<TModel, TGroupKey> ColumnWidth(Size width)
  {
    _columnWidth = width;
    return this;
  }

  public override object? Build()
  {
    if (!records.Any()) return _empty ?? new Fragment();

    var groupByFunc = groupBySelector.Compile();
    var grouped = records.GroupBy(groupByFunc);

    IEnumerable<IGrouping<TGroupKey, TModel>> orderedGroups;
    if (_columnOrderBySelector != null)
    {
      var columnOrderFunc = _columnOrderBySelector.Compile();
      orderedGroups = _columnOrderDescending
          ? grouped.OrderByDescending(g => columnOrderFunc(g.First()))
          : grouped.OrderBy(g => columnOrderFunc(g.First()));
    }
    else
    {
      orderedGroups = grouped;
    }

    var cards = orderedGroups.SelectMany(group =>
    {
      return group.Select(item =>
      {
        object content = _customCardRenderer != null ? _customCardRenderer(item) : "";

        // Apply simulated gap
        if (_gap > 0)
        {
          content = Layout.Vertical().Padding(0, 0, 0, _gap).Add(content);
        }

        var card = new KanbanCard(content);

        var cardId = cardIdSelector?.Compile().Invoke(item);
        if (cardId != null) card = card with { CardId = cardId };

        var priority = cardOrderSelector?.Compile().Invoke(item);
        // Assuming priority can be int
        if (priority is int p) card = card with { Priority = p };

        card = card with { Column = group.Key };
        return card;
      });
    }).ToArray();

    // Use reflection or try setting properties directly if Ivy.Kanban supports them. 
    // Based on KanbanBuilder source, Ivy.Kanban is a record with init props.
    // Assuming Ivy.Kanban has Gap. If not, this might fail to compile, but we'll see.

    var kanban = new Ivy.Kanban(cards) with
    {
      ShowCounts = _showCounts,
      Width = _width ?? Size.Full(),
      Height = _height ?? Size.Full(),
      ColumnWidth = _columnWidth
    };

    if (_onMove != null)
    {
      kanban = kanban with
      {
        OnCardMove = e =>
        {
          if (e.Value.ToColumn is TGroupKey groupKey)
            return _onMove(new Event<Ivy.Kanban, (object?, TGroupKey, int?)>(e.EventName, e.Sender, (e.Value.CardId, groupKey, e.Value.TargetIndex)));
          return ValueTask.CompletedTask;
        }
      };
    }
    return kanban;
  }
}
