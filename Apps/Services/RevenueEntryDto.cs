namespace ArtistInsightTool.Apps.Services;

public record RevenueEntryDto(
    int Id,
    DateTime RevenueDate,
    decimal Amount,
    string? Description,
    string Source,
    string? Integration,
    string? TrackTitle,
    string? AlbumTitle,
    string? ArtistName,
    string? ImportTemplateName,
    string? JsonData // For annex data
);
