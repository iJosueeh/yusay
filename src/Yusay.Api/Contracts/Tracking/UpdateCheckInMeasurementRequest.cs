namespace Yusay.Api.Contracts.Tracking;

public sealed record UpdateCheckInMeasurementRequest(Guid DimensionId, int Value);
