/*
The count of campaigns that are active during the selected date range.
COUNT(Campaign.Id WHERE Campaign.StartDate <= EndDate AND Campaign.EndDate >= StartDate)
*/
namespace ArtistInsightTool.Apps.Views;

public class NumberOfActiveCampaignsMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
    public override object Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();

        async Task<MetricRecord> CalculateNumberOfActiveCampaigns()
        {
            await using var db = factory.CreateDbContext();

            var currentPeriodActiveCampaigns = await db.Campaigns
                .Where(c => c.StartDate <= toDate && c.EndDate >= fromDate)
                .CountAsync();

            var periodLength = toDate - fromDate;
            var previousFromDate = fromDate.AddDays(-periodLength.TotalDays);
            var previousToDate = fromDate.AddDays(-1);

            var previousPeriodActiveCampaigns = await db.Campaigns
                .Where(c => c.StartDate <= previousToDate && c.EndDate >= previousFromDate)
                .CountAsync();

            if (previousPeriodActiveCampaigns == 0)
            {
                return new MetricRecord(
                    MetricFormatted: currentPeriodActiveCampaigns.ToString("N0"),
                    TrendComparedToPreviousPeriod: null,
                    GoalAchieved: null,
                    GoalFormatted: null
                );
            }

            double? trend = ((double)currentPeriodActiveCampaigns - previousPeriodActiveCampaigns) / previousPeriodActiveCampaigns;

            var goal = previousPeriodActiveCampaigns * 1.1;
            double? goalAchievement = goal > 0 ? (double?)(currentPeriodActiveCampaigns / goal ): null;

            return new MetricRecord(
                MetricFormatted: currentPeriodActiveCampaigns.ToString("N0"),
                TrendComparedToPreviousPeriod: trend,
                GoalAchieved: goalAchievement,
                GoalFormatted: goal.ToString("N0")
            );
        }

        return new MetricView(
            "Number of Active Campaigns",
            Icons.Activity,
            CalculateNumberOfActiveCampaigns
        );
    }
}