using Dapper;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Infrastructure.Assessment.Repositories;

public sealed class InstrumentCatalogRepository(IDbConnectionFactory connectionFactory) : IInstrumentCatalogRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<PublishedInstrumentSummaryDto>> ListPublishedInstrumentsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                i.instrument_id AS InstrumentId,
                i.code AS Code,
                i.name AS Name,
                i.description AS Description,
                i.purpose AS Purpose,
                v.instrument_version_id AS InstrumentVersionId,
                v.version AS Version,
                v.source_description AS SourceDescription,
                v.population AS Population,
                v.administration_conditions AS AdministrationConditions,
                v.license_information AS LicenseInformation,
                v.limitations AS Limitations
            FROM yusay.instrument i
            JOIN yusay.instrument_version v 
              ON v.instrument_id = i.instrument_id 
             AND v.status = 'PUBLISHED'
            ORDER BY i.name ASC, i.instrument_id ASC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PublishedSummaryRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.Select(r => new PublishedInstrumentSummaryDto(
            r.InstrumentId,
            r.Code,
            r.Name,
            r.Description,
            r.Purpose,
            new PublishedVersionSummaryDto(
                r.InstrumentVersionId,
                r.Version,
                r.SourceDescription,
                r.Population,
                r.AdministrationConditions,
                r.LicenseInformation,
                r.Limitations))).ToList();
    }

    public async Task<PublishedInstrumentDetailsDto?> GetPublishedInstrumentDetailsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                i.instrument_id AS InstrumentId,
                i.code AS Code,
                i.name AS Name,
                i.description AS Description,
                i.purpose AS Purpose,
                v.instrument_version_id AS InstrumentVersionId,
                v.version AS Version,
                v.source_description AS SourceDescription,
                v.population AS Population,
                v.administration_conditions AS AdministrationConditions,
                v.license_information AS LicenseInformation,
                v.limitations AS Limitations,
                r.reference_order AS ReferenceOrder,
                r.citation AS Citation,
                r.url AS Url
            FROM yusay.instrument i
            JOIN yusay.instrument_version v 
              ON v.instrument_id = i.instrument_id 
             AND v.status = 'PUBLISHED'
            LEFT JOIN yusay.instrument_version_reference r 
              ON r.instrument_version_id = v.instrument_version_id
            WHERE i.instrument_id = @InstrumentId
            ORDER BY r.reference_order ASC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<PublishedDetailsRow>(
            new CommandDefinition(sql, new { InstrumentId = instrumentId }, cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
        {
            return null;
        }

        var first = rows[0];
        var references = rows
            .Where(r => r.ReferenceOrder.HasValue)
            .Select(r => new PublishedInstrumentReferenceDto(
                r.ReferenceOrder!.Value,
                r.Citation!,
                r.Url))
            .ToList();

        return new PublishedInstrumentDetailsDto(
            first.InstrumentId,
            first.Code,
            first.Name,
            first.Description,
            first.Purpose,
            new PublishedVersionDetailsDto(
                first.InstrumentVersionId,
                first.Version,
                first.SourceDescription,
                first.Population,
                first.AdministrationConditions,
                first.LicenseInformation,
                first.Limitations,
                references));
    }

    private sealed class PublishedSummaryRow
    {
        public Guid InstrumentId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public Guid InstrumentVersionId { get; init; }
        public int Version { get; init; }
        public string SourceDescription { get; init; } = string.Empty;
        public string Population { get; init; } = string.Empty;
        public string? AdministrationConditions { get; init; }
        public string? LicenseInformation { get; init; }
        public string Limitations { get; init; } = string.Empty;
    }

    private sealed class PublishedDetailsRow
    {
        public Guid InstrumentId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public Guid InstrumentVersionId { get; init; }
        public int Version { get; init; }
        public string SourceDescription { get; init; } = string.Empty;
        public string Population { get; init; } = string.Empty;
        public string? AdministrationConditions { get; init; }
        public string? LicenseInformation { get; init; }
        public string Limitations { get; init; } = string.Empty;
        public int? ReferenceOrder { get; init; }
        public string? Citation { get; init; }
        public string? Url { get; init; }
    }
}
