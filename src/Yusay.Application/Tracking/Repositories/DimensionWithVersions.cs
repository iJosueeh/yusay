using Yusay.Domain.Tracking.Entities;

namespace Yusay.Application.Tracking.Repositories;

public sealed record DimensionWithVersions(
    Dimension Dimension,
    IReadOnlyList<DimensionVersion> Versions);
