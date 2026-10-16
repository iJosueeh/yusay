namespace Yusay.Application.Tracking.Commands.CreateDimension;

public sealed record CreateDimensionCommand(
    string Code,
    string Name,
    string Description);
