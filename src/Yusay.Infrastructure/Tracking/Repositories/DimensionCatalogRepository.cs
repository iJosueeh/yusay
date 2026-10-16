using System.Data.Common;
using System.Text.Json;
using Dapper;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Infrastructure.Tracking.Repositories;

// B4 de RF-021: lecturas del catálogo Dimension/DimensionVersion/DimensionAnchor con
// instantánea única (json_agg anidado, patrón de CheckInRepository): sin N+1 y sin
// bloqueos. Cada operación fija su predicado de visibilidad y rehidrata las entidades de
// dominio por sus fábricas Rehydrate. El orden garantizado es code ASC, version ASC y
// value ASC sobre los índices existentes (uq_dimension_code,
// uq_dimension_version_dimension_id_version y la PK de dimension_anchor).
public sealed class DimensionCatalogRepository(IDbConnectionFactory connectionFactory) : IDimensionCatalogRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public Task<IReadOnlyList<DimensionWithVersions>> ListPublishedAsync(
        CancellationToken cancellationToken = default) =>
        QueryListAsync(
            "v.status = 'ACTIVE'",
            "EXISTS (SELECT 1 FROM yusay.dimension_version dv" +
            " WHERE dv.dimension_id = d.dimension_id AND dv.status = 'ACTIVE')",
            cancellationToken);

    public Task<DimensionWithVersions?> GetWithHistoryAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync(
            "v.status IN ('ACTIVE', 'RETIRED')",
            "EXISTS (SELECT 1 FROM yusay.dimension_version dv" +
            " WHERE dv.dimension_id = d.dimension_id AND dv.status IN ('ACTIVE', 'RETIRED'))",
            dimensionId,
            cancellationToken);

    public Task<IReadOnlyList<DimensionWithVersions>> ListCompleteAsync(
        CancellationToken cancellationToken = default) =>
        QueryListAsync("TRUE", "TRUE", cancellationToken);

    private async Task<IReadOnlyList<DimensionWithVersions>> QueryListAsync(
        string versionPredicate,
        string visibilityPredicate,
        CancellationToken cancellationToken)
    {
        var sql = BuildSql(versionPredicate, visibilityPredicate, isDetail: false);

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<DimensionRow>(new CommandDefinition(
                sql, cancellationToken: cancellationToken));

            return rows.Select(MapRow).ToArray();
        }, cancellationToken);
    }

    private async Task<DimensionWithVersions?> QuerySingleOrDefaultAsync(
        string versionPredicate,
        string visibilityPredicate,
        Guid dimensionId,
        CancellationToken cancellationToken)
    {
        var sql = BuildSql(versionPredicate, visibilityPredicate, isDetail: true);

        return await ExecuteAsync(async conn =>
        {
            var row = await conn.QuerySingleOrDefaultAsync<DimensionRow>(new CommandDefinition(
                sql, new { DimensionId = dimensionId }, cancellationToken: cancellationToken));

            return row is null ? null : MapRow(row);
        }, cancellationToken);
    }

    private async Task<T> ExecuteAsync<T>(
        Func<DbConnection, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await action(connection);
    }

    private static string BuildSql(
        string versionPredicate,
        string visibilityPredicate,
        bool isDetail)
    {
        var dimensionPredicate = isDetail
            ? "d.dimension_id = @DimensionId AND " + visibilityPredicate
            : visibilityPredicate;

        return $"""
            SELECT
                d.dimension_id,
                d.code,
                d.name,
                d.description,
                COALESCE((
                    SELECT json_agg(json_build_object(
                        'dimensionVersionId', v.dimension_version_id,
                        'version', v.version,
                        'status', v.status,
                        'definition', v.definition,
                        'minValue', v.min_value,
                        'maxValue', v.max_value,
                        'step', v.step,
                        'anchors', COALESCE((
                            SELECT json_agg(json_build_object('value', a.value, 'label', a.label)
                                            ORDER BY a.value)
                            FROM yusay.dimension_anchor a
                            WHERE a.dimension_version_id = v.dimension_version_id
                        ), '[]'::json)
                    ) ORDER BY v.version)
                    FROM yusay.dimension_version v
                    WHERE v.dimension_id = d.dimension_id
                      AND {versionPredicate}
                ), '[]'::json) AS versions
            FROM yusay.dimension d
            WHERE {dimensionPredicate}
            ORDER BY d.code ASC;
            """;
    }

    // Rehidrata por las fábricas de dominio: validan lo que la base garantiza
    // (definición no vacía, escala coherente y anclas sin duplicados dentro de la
    // escala); un fallo aquí indica corrupción de datos y no debe silenciarse.
    private static DimensionWithVersions MapRow(DimensionRow row)
    {
        var versionRows = JsonSerializer.Deserialize<VersionRow[]>(row.Versions, JsonOptions) ?? [];

        var dimension = Dimension.Rehydrate(
            row.Dimension_id,
            (DimensionCode)row.Code,
            row.Name,
            row.Description);

        var versions = versionRows
            .Select(version => DimensionVersion.Rehydrate(
                version.DimensionVersionId,
                row.Dimension_id,
                version.Version,
                DimensionScale.Create(version.MinValue, version.MaxValue, version.Step),
                version.Definition,
                ParseStatus(version.Status),
                (version.Anchors ?? [])
                    .Select(anchor => DimensionAnchor.Create(anchor.Value, anchor.Label))))
            .ToArray();

        return new DimensionWithVersions(dimension, versions);
    }

    // ck_dimension_version_status (V004) garantiza los tres valores; cualquier otro
    // indica corrupción de datos y no debe silenciarse en la lectura.
    private static DimensionVersionStatus ParseStatus(string status) => status switch
    {
        "DRAFT" => DimensionVersionStatus.Draft,
        "ACTIVE" => DimensionVersionStatus.Active,
        "RETIRED" => DimensionVersionStatus.Retired,
        _ => throw new InvalidOperationException(
            $"Valor de estado no reconocido en yusay.dimension_version: '{status}'.")
    };

    private sealed class DimensionRow
    {
        public Guid Dimension_id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Versions { get; init; } = string.Empty;
    }

    private sealed record VersionRow(
        Guid DimensionVersionId,
        int Version,
        string Status,
        string Definition,
        int MinValue,
        int MaxValue,
        int Step,
        IReadOnlyList<AnchorRow>? Anchors);

    private sealed record AnchorRow(int Value, string Label);
}
