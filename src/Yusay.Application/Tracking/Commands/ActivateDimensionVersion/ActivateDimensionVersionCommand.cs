namespace Yusay.Application.Tracking.Commands.ActivateDimensionVersion;

public sealed record ActivateDimensionVersionCommand(
    Guid DimensionId,
    Guid DimensionVersionId);
