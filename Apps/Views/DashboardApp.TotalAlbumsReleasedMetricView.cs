/*
The total number of albums released during the selected date range.
COUNT(Album.Id WHERE Album.ReleaseDate BETWEEN StartDate AND EndDate)
*/
namespace ArtistInsightTool.Apps.Views;

public class TotalAlbumsReleasedMetricView(DateTime fromDate, DateTime toDate) : ViewBase
{
    public override object Build()
    {
        var factory = UseService<ArtistInsightToolContextFactory>();

        async Task<MetricRecord> CalculateTotalAlbumsReleased()
        {
            await using var db = factory.CreateDbContext();

            var currentPeriodAlbums = await db.Albums
                .Where(a => a.ReleaseDate >= fromDate && a.ReleaseDate <= toDate)
                .CountAsync();

            var periodLength = toDate - fromDate;
            var previousFromDate = fromDate.AddDays(-periodLength.TotalDays);
            var previousToDate = fromDate.AddDays(-1);

            var previousPeriodAlbums = await db.Albums
                .Where(a => a.ReleaseDate >= previousFromDate && a.ReleaseDate <= previousToDate)
                .CountAsync();

            if (previousPeriodAlbums == 0)
            {
                return new MetricRecord(
                    MetricFormatted: currentPeriodAlbums.ToString("N0"),
                    TrendComparedToPreviousPeriod: null,
                    GoalAchieved: null,
                    GoalFormatted: null
                );
            }

            double? trend = ((double)currentPeriodAlbums - previousPeriodAlbums) / previousPeriodAlbums;

            var goal = previousPeriodAlbums * 1.1;
            double? goalAchievement = goal > 0 ? (double?)(currentPeriodAlbums / goal ): null;

            return new MetricRecord(
                MetricFormatted: currentPeriodAlbums.ToString("N0"),
                TrendComparedToPreviousPeriod: trend,
                GoalAchieved: goalAchievement,
                GoalFormatted: goal.ToString("N0")
            );
        }

        return new MetricView(
            "Total Albums Released",
            Icons.Disc,
            CalculateTotalAlbumsReleased
        );
    }
}