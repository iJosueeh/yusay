namespace Yusay.Application.Tracking.Repositories;

public sealed record StoredDimensionScale(
    Guid DimensionVersionId,
    int MinValue,
    int MaxValue,
    int Step);
