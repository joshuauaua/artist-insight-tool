namespace ArtistInsightTool.Apps.Services;

public record RevenueEntryDto(
    int Id,
    DateTime RevenueDate,
    decimal Amount,
    string? Description,
    string Source,
    string? Integration,

    string? ImportTemplateName,
    string? JsonData, // For annex data
    DateTime? UploadDate
);
