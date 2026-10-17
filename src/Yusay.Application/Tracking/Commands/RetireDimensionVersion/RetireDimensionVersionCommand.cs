namespace Yusay.Application.Tracking.Commands.RetireDimensionVersion;

public sealed record RetireDimensionVersionCommand(
    Guid DimensionId,
    Guid DimensionVersionId);
