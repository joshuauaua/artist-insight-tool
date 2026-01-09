/*
The percentage change in revenue compared to the previous date range.
((SUM(RevenueEntry.Amount WHERE RevenueEntry.RevenueDate BETWEEN StartDate AND EndDate) - SUM(RevenueEntry.Amount WHERE RevenueEntry.RevenueDate BETWEEN PreviousStartDate AND PreviousEndDate)) / SUM(RevenueEntry.Amount WHERE RevenueEntry.RevenueDate BETWEEN PreviousStartDate AND PreviousEndDate)) * 100
*/
namespace ArtistInsightTool.Apps.Views;

public class RevenueGrowthRateMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
    public override object? Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();

        async Task<MetricRecord> CalculateRevenueGrowthRate()
        {
            await using var db = factory.CreateDbContext();

            var currentPeriodRevenue = await db.RevenueEntries
                .Where(re => re.RevenueDate >= fromDate && re.RevenueDate <= toDate)
                .SumAsync(re => (double)re.Amount);

            var periodLength = toDate - fromDate;
            var previousFromDate = fromDate.AddDays(-periodLength.TotalDays);
            var previousToDate = fromDate.AddDays(-1);

            var previousPeriodRevenue = await db.RevenueEntries
                .Where(re => re.RevenueDate >= previousFromDate && re.RevenueDate <= previousToDate)
                .SumAsync(re => (double)re.Amount);

            if (previousPeriodRevenue == 0)
            {
                return new MetricRecord(
                    MetricFormatted: currentPeriodRevenue.ToString("N2"),
                    TrendComparedToPreviousPeriod: null,
                    GoalAchieved: null,
                    GoalFormatted: null
                );
            }

            double? trend = (currentPeriodRevenue - previousPeriodRevenue) / previousPeriodRevenue * 100;

            var goal = previousPeriodRevenue * 1.1;
            double? goalAchievement = goal > 0 ? (double?)(currentPeriodRevenue / goal ): null;

            return new MetricRecord(
                MetricFormatted: currentPeriodRevenue.ToString("N2"),
                TrendComparedToPreviousPeriod: trend,
                GoalAchieved: goalAchievement,
                GoalFormatted: goal.ToString("N2")
            );
        }

        return new MetricView(
            "Revenue Growth Rate",
            Icons.TrendingUp,
            CalculateRevenueGrowthRate
        );
    }
}