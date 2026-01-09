/*
The average duration of all tracks created by artists.
AVG(Track.Duration)
*/
namespace ArtistInsightTool.Apps.Views;

public class AverageTrackDurationMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
    public override object Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();

        async Task<MetricRecord> CalculateAverageTrackDuration()
        {
            await using var db = factory.CreateDbContext();

            var currentPeriodTracks = await db.Tracks
                .Where(t => t.CreatedAt >= fromDate && t.CreatedAt <= toDate && t.Duration.HasValue)
                .ToListAsync();

            var currentAverageDuration = currentPeriodTracks.Any()
                ? currentPeriodTracks.Average(t => (double)t.Duration!.Value)
                : 0.0;

            var periodLength = toDate - fromDate;
            var previousFromDate = fromDate.AddDays(-periodLength.TotalDays);
            var previousToDate = fromDate.AddDays(-1);

            var previousPeriodTracks = await db.Tracks
                .Where(t => t.CreatedAt >= previousFromDate && t.CreatedAt <= previousToDate && t.Duration.HasValue)
                .ToListAsync();

            var previousAverageDuration = previousPeriodTracks.Any()
                ? previousPeriodTracks.Average(t => (double)t.Duration!.Value)
                : 0.0;

            if (previousAverageDuration == 0.0)
            {
                return new MetricRecord(
                    MetricFormatted: currentAverageDuration.ToString("N2") + " seconds",
                    TrendComparedToPreviousPeriod: null,
                    GoalAchieved: null,
                    GoalFormatted: null
                );
            }

            double? trend = (currentAverageDuration - previousAverageDuration) / previousAverageDuration;

            var goal = previousAverageDuration * 1.1;
            double? goalAchievement = goal > 0 ? (double?)(currentAverageDuration / goal ): null;

            return new MetricRecord(
                MetricFormatted: currentAverageDuration.ToString("N2") + " seconds",
                TrendComparedToPreviousPeriod: trend,
                GoalAchieved: goalAchievement,
                GoalFormatted: goal.ToString("N2") + " seconds"
            );
        }

        return new MetricView(
            "Average Track Duration",
            Icons.Clock,
            CalculateAverageTrackDuration
        );
    }
}