using Yusay.Application.Tracking.Queries.GetCheckInById;

namespace Yusay.Application.Tracking.Queries.ListCheckIns;

public sealed record ListCheckInsResult(
    IReadOnlyList<GetCheckInByIdResult> Items,
    string? NextCursor);
