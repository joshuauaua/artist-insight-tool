/*
The average revenue generated per track during the selected date range.
SUM(RevenueEntry.Amount WHERE RevenueEntry.RevenueDate BETWEEN StartDate AND EndDate) / COUNT(DISTINCT RevenueEntry.TrackId)
*/
namespace ArtistInsightTool.Apps.Views;

public class RevenuePerTrackMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
  public override object Build()
  {
    var factory = UseService<ArtistInsightToolContextFactory>();

    async Task<MetricRecord> CalculateRevenuePerTrack()
    {
      await using var db = factory.CreateDbContext();

      var currentPeriodRevenueEntries = await db.RevenueEntries
          .Where(re => re.RevenueDate >= fromDate && re.RevenueDate <= toDate)
          .ToListAsync();

      var currentPeriodRevenue = currentPeriodRevenueEntries.Sum(re => (double)re.Amount);
      var currentPeriodTrackCount = currentPeriodRevenueEntries
          .Where(re => re.TrackId.HasValue)
          .Select(re => re.TrackId!.Value)
          .Distinct()
          .Count();

      var currentRevenuePerTrack = currentPeriodTrackCount > 0
          ? currentPeriodRevenue / currentPeriodTrackCount
          : 0.0;

      var periodLength = toDate - fromDate;
      var previousFromDate = fromDate.AddDays(-periodLength.TotalDays);
      var previousToDate = fromDate.AddDays(-1);

      var previousPeriodRevenueEntries = await db.RevenueEntries
          .Where(re => re.RevenueDate >= previousFromDate && re.RevenueDate <= previousToDate)
          .ToListAsync();

      var previousPeriodRevenue = previousPeriodRevenueEntries.Sum(re => (double)re.Amount);
      var previousPeriodTrackCount = previousPeriodRevenueEntries
          .Where(re => re.TrackId.HasValue)
          .Select(re => re.TrackId!.Value)
          .Distinct()
          .Count();

      var previousRevenuePerTrack = previousPeriodTrackCount > 0
          ? previousPeriodRevenue / previousPeriodTrackCount
          : 0.0;

      if (previousRevenuePerTrack == 0)
      {
        return new MetricRecord(
            MetricFormatted: currentRevenuePerTrack.ToString("C2"),
            TrendComparedToPreviousPeriod: null,
            GoalAchieved: null,
            GoalFormatted: null
        );
      }

      double? trend = (currentRevenuePerTrack - previousRevenuePerTrack) / previousRevenuePerTrack;
      var goal = previousRevenuePerTrack * 1.1;
      double? goalAchievement = goal > 0 ? (double?)(currentRevenuePerTrack / goal) : null;

      return new MetricRecord(
          MetricFormatted: currentRevenuePerTrack.ToString("C2"),
          TrendComparedToPreviousPeriod: trend,
          GoalAchieved: goalAchievement,
          GoalFormatted: goal.ToString("C2")
      );
    }

    return new MetricView(
        "Revenue Per Track",
        null,
        CalculateRevenuePerTrack
    );
  }
}