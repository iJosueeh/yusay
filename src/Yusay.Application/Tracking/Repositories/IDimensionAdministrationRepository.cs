using System.Data.Common;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Application.Tracking.Repositories;

// traducir (500).
public interface IDimensionAdministrationRepository
{
    public const string DuplicateCodeConflictMessage =
        "Ya existe una Dimensión con el código proporcionado.";

    public const string DuplicateVersionNumberConflictMessage =
        "Ya existe una versión de la Dimensión con el número proporcionado.";

    Task<bool> CodeExistsAsync(
        string code,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Dimension dimension,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<Dimension?> GetByIdForUpdateAsync(
        Guid dimensionId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task UpdateDetailsAsync(
        Dimension dimension,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<bool> VersionNumberExistsAsync(
        Guid dimensionId,
        int version,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task AddVersionAsync(
        DimensionVersion version,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<DimensionVersion?> GetVersionForUpdateAsync(
        Guid dimensionId,
        Guid dimensionVersionId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task UpdateVersionDetailsAsync(
        Guid dimensionVersionId,
        string definition,
        int minValue,
        int maxValue,
        int step,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task DeleteAnchorsAsync(
        Guid dimensionVersionId,
        IReadOnlyList<int> values,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task UpdateAnchorLabelAsync(
        Guid dimensionVersionId,
        int value,
        string label,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task InsertAnchorsAsync(
        Guid dimensionVersionId,
        IReadOnlyList<DimensionAnchor> anchors,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
