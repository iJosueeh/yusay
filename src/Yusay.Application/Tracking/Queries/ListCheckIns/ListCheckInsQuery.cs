namespace Yusay.Application.Tracking.Queries.ListCheckIns;

public sealed record ListCheckInsQuery(
    int? Limit,
    string? Cursor);
