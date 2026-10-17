namespace Yusay.Application.Tracking.Commands.UpdateDimensionVersion;

public sealed record UpdateDimensionVersionCommand(
    Guid DimensionId,
    Guid DimensionVersionId,
    string? Definition,
    int? MinValue,
    int? MaxValue,
    int? Step,
    IReadOnlyList<AnchorInput>? Anchors);
