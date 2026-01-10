using ArtistInsightTool.Apps.Views;

namespace ArtistInsightTool.Apps;

[App(icon: Icons.ChartBar, path: ["Apps"])]
public class DashboardApp : ViewBase
{
  public override object? Build()
  {
    var range = this.UseState(() => (fromDate: DateTime.Today.Date.AddDays(-30), toDate: DateTime.Today.Date));

    var header = Layout.Horizontal().Align(Align.Right)
                | range.ToDateRangeInput();

    var fromDate = range.Value.fromDate;
    var toDate = range.Value.toDate;

    var metrics =
            Layout.Grid().Columns(4)
| new TotalRevenueMetricView(fromDate, toDate).Key(fromDate, toDate) | new AverageTrackDurationMetricView(fromDate, toDate).Key(fromDate, toDate) | new TotalTracksCreatedMetricView(fromDate, toDate).Key(fromDate, toDate) | new RevenuePerTrackMetricView(fromDate, toDate).Key(fromDate, toDate) | new TotalAlbumsReleasedMetricView(fromDate, toDate).Key(fromDate, toDate) | new RevenueGrowthRateMetricView(fromDate, toDate).Key(fromDate, toDate) | new TopRevenueSourceContributionMetricView(fromDate, toDate).Key(fromDate, toDate);

    var charts =
            Layout.Grid().Columns(3)
| new DailyRevenueTrendLineChartView(fromDate, toDate).Key(fromDate, toDate) | new RevenueBySourcePieChartView(fromDate, toDate).Key(fromDate, toDate) | new TopPerformingTracksLineChartView(fromDate, toDate).Key(fromDate, toDate) | new AlbumRevenueDistributionPieChartView(fromDate, toDate).Key(fromDate, toDate);

    return Layout.Horizontal().Align(Align.Center) |
           new HeaderLayout(header, Layout.Vertical()
                        | metrics
                        | charts
            ).Width(Size.Full().Max(300));
  }
}