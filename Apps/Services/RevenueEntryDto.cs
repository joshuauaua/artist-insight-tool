namespace ArtistInsightTool.Apps.Services;

public record RevenueEntryDto(
    int Id,
    DateTime RevenueDate,
    decimal Amount,
    string? Description,
    string Source,
    string? Integration,
    string? ArtistName,
    string? ImportTemplateName,
    string? JsonData = null,
    string Category = "Other",
    DateTime? UploadDate = null,
    int? Year = null,
    string? Quarter = null
);
