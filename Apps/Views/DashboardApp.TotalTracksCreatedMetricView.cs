/*
The total number of tracks created by artists during the selected date range.
COUNT(Track.Id WHERE Track.CreatedAt BETWEEN StartDate AND EndDate)
*/
namespace ArtistInsightTool.Apps.Views;

public class TotalTracksCreatedMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
    public override object Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();

        async Task<MetricRecord> CalculateTotalTracksCreated()
        {
            await using var db = factory.CreateDbContext();

            var currentPeriodTracks = await db.Tracks
                .Where(t => t.CreatedAt >= fromDate && t.CreatedAt <= toDate)
                .CountAsync();

            var periodLength = toDate - fromDate;
            var previousFromDate = fromDate.AddDays(-periodLength.TotalDays);
            var previousToDate = fromDate.AddDays(-1);

            var previousPeriodTracks = await db.Tracks
                .Where(t => t.CreatedAt >= previousFromDate && t.CreatedAt <= previousToDate)
                .CountAsync();

            if (previousPeriodTracks == 0)
            {
                return new MetricRecord(
                    MetricFormatted: currentPeriodTracks.ToString("N0"),
                    TrendComparedToPreviousPeriod: null,
                    GoalAchieved: null,
                    GoalFormatted: null
                );
            }

            double? trend = ((double)currentPeriodTracks - previousPeriodTracks) / previousPeriodTracks;

            var goal = previousPeriodTracks * 1.1;
            double? goalAchievement = goal > 0 ? (double?)(currentPeriodTracks / goal ): null;

            return new MetricRecord(
                MetricFormatted: currentPeriodTracks.ToString("N0"),
                TrendComparedToPreviousPeriod: trend,
                GoalAchieved: goalAchievement,
                GoalFormatted: goal.ToString("N0")
            );
        }

        return new MetricView(
            "Total Tracks Created",
            Icons.Music,
            CalculateTotalTracksCreated
        );
    }
}