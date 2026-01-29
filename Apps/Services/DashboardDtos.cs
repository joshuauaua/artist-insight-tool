namespace ArtistInsightTool.Apps.Services;

public record MetricDto(
    string Value,
    double? Trend = null,
    double? GoalProgress = null,
    string? GoalValue = null,
    double? NumericValue = null
);

public record PieChartSegmentDto(string Label, double Value);

public record LineChartPointDto(DateTime Date, double Value);

public record TrackPerformanceDto(string TrackName, double Revenue, int Streams);

public record TopTrackPointDto(string TrackTitle, DateTime Date, double Revenue);
