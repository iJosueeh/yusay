using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Application.Tracking.Repositories;

public sealed record StoredDimensionScale(Guid DimensionVersionId, DimensionScale Scale);
