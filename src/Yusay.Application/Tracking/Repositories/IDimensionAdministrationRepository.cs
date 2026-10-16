using System.Data.Common;
using Yusay.Domain.Tracking.Entities;

namespace Yusay.Application.Tracking.Repositories;

// B5a de RF-021: escritura administrativa de Dimension (alta y edición de nombre y
// descripción). code es inmutable tras el alta: solo se inserta, nunca se actualiza. La
// lectura para edición se realiza con SELECT FOR UPDATE dentro de la transacción del caso
// de uso, de modo que la detección de cambios efectivos y la escritura observan el mismo
// estado bloqueado (última escritura gana; una fila no puede desaparecer tras el bloqueo,
// por lo que la actualización no necesita comprobación de filas afectadas). La traducción
// de SQLSTATE 23505 queda acotada a uq_dimension_code: el repositorio la convierte en
// ConflictException con el mismo mensaje del pre-check; cualquier otro SQLSTATE se
// propaga sin traducción (500).
public interface IDimensionAdministrationRepository
{
    /// <summary>Mensaje único de conflicto por código duplicado (pre-check y 23505).</summary>
    public const string DuplicateCodeConflictMessage =
        "Ya existe una Dimensión con el código proporcionado.";

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
}
