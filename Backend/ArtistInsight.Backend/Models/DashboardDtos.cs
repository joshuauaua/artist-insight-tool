namespace ArtistInsight.Backend.Models;

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

public record RevenueEntryDto(
    int Id,
    DateTime RevenueDate,
    decimal Amount,
    string? Description,
    string Source, // DescriptionText
    string? Integration,
    string? ArtistName,
    string? ImportTemplateName,
    string? JsonData = null,
    string Category = "Other",
    DateTime? UploadDate = null,
    int? Year = null,
    string? Quarter = null
);
