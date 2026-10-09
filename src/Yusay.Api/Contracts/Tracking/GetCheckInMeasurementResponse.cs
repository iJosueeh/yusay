namespace Yusay.Api.Contracts.Tracking;

public sealed record GetCheckInMeasurementResponse(
    Guid DimensionId,
    Guid DimensionVersionId,
    int Value);
