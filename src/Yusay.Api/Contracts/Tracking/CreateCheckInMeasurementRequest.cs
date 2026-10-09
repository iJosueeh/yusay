namespace Yusay.Api.Contracts.Tracking;

public sealed record CreateCheckInMeasurementRequest(Guid DimensionId, int Value);
