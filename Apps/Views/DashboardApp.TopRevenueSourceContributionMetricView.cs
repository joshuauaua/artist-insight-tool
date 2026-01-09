/*
The percentage of total revenue contributed by the top revenue source during the selected date range.
(MAX(SUM(RevenueEntry.Amount WHERE RevenueEntry.SourceId = RevenueSource.Id AND RevenueEntry.RevenueDate BETWEEN StartDate AND EndDate)) / SUM(RevenueEntry.Amount WHERE RevenueEntry.RevenueDate BETWEEN StartDate AND EndDate)) * 100
*/
namespace ArtistInsightTool.Apps.Views;

public class TopRevenueSourceContributionMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();

        async Task<MetricRecord> CalculateTopRevenueSourceContribution()
        {
            await using var db = factory.CreateDbContext();

            var currentPeriodRevenueEntries = await db.RevenueEntries
                .Where(re => re.RevenueDate >= fromDate && re.RevenueDate <= toDate)
                .Include(re => re.Source)
                .ToListAsync();

            var totalRevenue = currentPeriodRevenueEntries.Sum(re => (double)re.Amount);

            var topRevenueSourceContribution = currentPeriodRevenueEntries
                .GroupBy(re => re.SourceId)
                .Select(g => new { SourceId = g.Key, Total = g.Sum(re => (double)re.Amount) })
                .OrderByDescending(g => g.Total)
                .FirstOrDefault();

            var topContributionPercentage = topRevenueSourceContribution != null && totalRevenue > 0
                ? (topRevenueSourceContribution.Total / totalRevenue) * 100
                : 0.0;

            var periodLength = toDate - fromDate;
            var previousFromDate = fromDate.AddDays(-periodLength.TotalDays);
            var previousToDate = fromDate.AddDays(-1);

            var previousPeriodRevenueEntries = await db.RevenueEntries
                .Where(re => re.RevenueDate >= previousFromDate && re.RevenueDate <= previousToDate)
                .ToListAsync();

            var previousTotalRevenue = previousPeriodRevenueEntries.Sum(re => (double)re.Amount);

            var previousTopRevenueSourceContribution = previousPeriodRevenueEntries
                .GroupBy(re => re.SourceId)
                .Select(g => new { SourceId = g.Key, Total = g.Sum(re => (double)re.Amount) })
                .OrderByDescending(g => g.Total)
                .FirstOrDefault();

            var previousTopContributionPercentage = previousTopRevenueSourceContribution != null && previousTotalRevenue > 0
                ? (previousTopRevenueSourceContribution.Total / previousTotalRevenue) * 100
                : 0.0;

            double? trend = previousTopContributionPercentage > 0
                ? (double?)((topContributionPercentage - previousTopContributionPercentage) / previousTopContributionPercentage
)                : null;

            var goal = previousTopContributionPercentage * 1.1;
            double? goalAchievement = goal > 0 ? topContributionPercentage / goal : null;

            return new MetricRecord(
                MetricFormatted: topContributionPercentage.ToString("N2") + "%",
                TrendComparedToPreviousPeriod: trend,
                GoalAchieved: goalAchievement,
                GoalFormatted: goal.ToString("N2") + "%"
            );
        }

        return new MetricView(
            "Top Revenue Source Contribution",
            null,
            CalculateTopRevenueSourceContribution
        );
    }
}