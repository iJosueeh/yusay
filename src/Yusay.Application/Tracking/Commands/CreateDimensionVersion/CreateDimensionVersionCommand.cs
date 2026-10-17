namespace Yusay.Application.Tracking.Commands.CreateDimensionVersion;

public sealed record CreateDimensionVersionCommand(
    Guid DimensionId,
    int Version,
    string Definition,
    int MinValue,
    int MaxValue,
    int Step,
    IReadOnlyList<AnchorInput>? Anchors);
