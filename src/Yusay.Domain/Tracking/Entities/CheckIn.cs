using Yusay.Domain.Common;

namespace Yusay.Domain.Tracking.Entities;

public sealed class CheckIn
{
    public Guid CheckInId { get; }
    public Guid UserId { get; }
    public DateTimeOffset RecordedAt { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; }
    public int Revision { get; }
    public string? Note { get; }
    public IReadOnlyList<Measurement> Measurements { get; }

    private CheckIn(
        Guid checkInId,
        Guid userId,
        DateTimeOffset recordedAt,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt,
        int revision,
        string? note,
        IReadOnlyList<Measurement> measurements)
    {
        CheckInId = checkInId;
        UserId = userId;
        RecordedAt = recordedAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Revision = revision;
        Note = note;
        Measurements = measurements;
    }

    public static CheckIn Create(
        Guid userId,
        DateTimeOffset recordedAt,
        DateTimeOffset createdAt,
        string? note,
        IReadOnlyList<Measurement> measurements,
        Guid? checkInId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("El CheckIn debe pertenecer exactamente a un User.");
        }

        ValidateMeasurements(measurements);

        return new CheckIn(
            checkInId ?? Guid.NewGuid(),
            userId,
            recordedAt,
            createdAt,
            updatedAt: null,
            revision: 1,
            note: NormalizeNote(note),
            measurements: measurements.ToArray());
    }

    public static CheckIn Rehydrate(
        Guid checkInId,
        Guid userId,
        DateTimeOffset recordedAt,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt,
        int revision,
        string? note,
        IReadOnlyList<Measurement> measurements)
    {
        ValidateMeasurements(measurements);

        return new CheckIn(
            checkInId,
            userId,
            recordedAt,
            createdAt,
            updatedAt,
            revision,
            NormalizeNote(note),
            measurements.ToArray());
    }

    private static void ValidateMeasurements(IReadOnlyList<Measurement>? measurements)
    {
        if (measurements is null || measurements.Count == 0)
        {
            throw new DomainException("Un CheckIn válido contiene al menos una Measurement.");
        }

        var hasDuplicateDimension = measurements
            .GroupBy(measurement => measurement.DimensionId)
            .Any(group => group.Count() > 1);

        if (hasDuplicateDimension)
        {
            throw new DomainException("Un CheckIn no puede contener más de una Measurement de la misma Dimensión.");
        }
    }

    private static string? NormalizeNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note;
}
