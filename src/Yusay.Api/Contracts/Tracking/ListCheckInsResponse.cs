namespace Yusay.Api.Contracts.Tracking;

public sealed record ListCheckInsResponse(
    IReadOnlyList<GetCheckInByIdResponse> Items,
    string? NextCursor);
