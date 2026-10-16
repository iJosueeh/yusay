using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Application.Tracking.Repositories;

public sealed record ActiveDimensionScale(Guid DimensionVersionId, DimensionScale Scale);
