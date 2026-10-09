namespace Yusay.Application.Tracking.Repositories;

public sealed record ActiveDimensionScale(
    Guid DimensionVersionId,
    int MinValue,
    int MaxValue,
    int Step);
