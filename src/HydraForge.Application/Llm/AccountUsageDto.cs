namespace HydraForge.Application.Llm;

public sealed record AccountUsageResponse(
    long TokensUsed,
    int TokensBudget,
    int ImagesUsed,
    int ImagesBudget,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    IReadOnlyList<RecentCallDto> RecentCalls
);

public sealed record RecentCallDto(
    string Feature,
    string Model,
    int Tokens,
    int Images,
    decimal Cost,
    DateTime Timestamp
);
