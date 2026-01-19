namespace ArtistInsight.Backend.Models;

public record MetricDto(
    string Value,
    double? Trend,
    double? GoalProgress,
    string? GoalValue
);

public record PieChartSegmentDto(string Label, double Value);

public record LineChartPointDto(DateTime Date, double Value);

public record TrackPerformanceDto(string TrackName, double Revenue, int Streams);

public record TopTrackPointDto(string TrackTitle, DateTime Date, double Revenue);
