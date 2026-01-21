using Ivy.Shared;

namespace ArtistInsightTool.Apps.Views;

public abstract class WrapperViewBase : ViewBase
{
  // A simple Query Hook implementation
  protected QueryResult<T> UseQuery<T>(string key, Func<Task<T>> fetcher)
  {
    var data = UseState<T?>(() => default);
    var isLoading = UseState(true);
    var error = UseState<string?>(() => null);
    var refetchTrigger = UseState(0);

    UseEffect(async () =>
    {
      isLoading.Set(true);
      try
      {
        var result = await fetcher();
        data.Set(result);
        error.Set((string?)null);
      }
      catch (Exception ex)
      {
        error.Set(ex.Message);
      }
      finally
      {
        isLoading.Set(false);
      }
    }, [refetchTrigger]);

    return new QueryResult<T>(data.Value, isLoading.Value, error.Value, () => refetchTrigger.Set(refetchTrigger.Value + 1));
  }
}

public class QueryResult<T>(T? data, bool isLoading, string? error, Action refetch)
{
  public T? Data { get; } = data;
  public bool IsLoading { get; } = isLoading;
  public string? Error { get; } = error;
  public void Refetch() => refetch();
}
