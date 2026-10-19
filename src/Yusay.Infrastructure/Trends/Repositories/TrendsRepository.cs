using Dapper;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Trends.Repositories;

namespace Yusay.Infrastructure.Trends.Repositories;

public sealed class TrendsRepository(IDbConnectionFactory connectionFactory) : ITrendsRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<ConceptHeaderDto?> GetDimensionHeaderAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT dimension_id AS Id, code AS Code, name AS Name
            FROM yusay.dimension
            WHERE dimension_id = @DimensionId;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ConceptHeaderDto>(new CommandDefinition(
            sql,
            new { DimensionId = dimensionId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ConceptVersionHeaderDto>> GetDimensionVersionsAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT dimension_version_id AS VersionId, version AS VersionNumber
            FROM yusay.dimension_version
            WHERE dimension_id = @DimensionId
            ORDER BY version ASC, dimension_version_id ASC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ConceptVersionHeaderDto>(new CommandDefinition(
            sql,
            new { DimensionId = dimensionId },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<IReadOnlyList<CompatibilityPairDto>> GetDimensionCompatibilityPairsAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT version_a_id AS VersionAId, version_b_id AS VersionBId
            FROM yusay.dimension_version_compatibility
            WHERE dimension_id = @DimensionId;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CompatibilityPairDto>(new CommandDefinition(
            sql,
            new { DimensionId = dimensionId },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<IReadOnlyList<DimensionMeasurementPointDto>> GetDimensionMeasurementsAsync(
        Guid userId,
        Guid dimensionId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.check_in_id AS CheckInId,
                m.dimension_version_id AS DimensionVersionId,
                dv.version AS VersionNumber,
                m.value AS Value,
                c.recorded_at AS RecordedAt
            FROM yusay.check_in c
            JOIN yusay.measurement m
                ON m.check_in_id = c.check_in_id
            JOIN yusay.dimension_version dv
                ON dv.dimension_id = m.dimension_id AND dv.dimension_version_id = m.dimension_version_id
            WHERE c.user_id = @UserId
              AND m.dimension_id = @DimensionId
              AND (@From::timestamptz IS NULL OR c.recorded_at >= @From::timestamptz)
              AND (@To::timestamptz IS NULL OR c.recorded_at <= @To::timestamptz)
            ORDER BY c.recorded_at ASC, c.check_in_id ASC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DimensionMeasurementRow>(new CommandDefinition(
            sql,
            new
            {
                UserId = userId,
                DimensionId = dimensionId,
                From = from,
                To = to
            },
            cancellationToken: cancellationToken));

        return rows.Select(r => new DimensionMeasurementPointDto(
            r.CheckInId,
            r.DimensionVersionId,
            r.VersionNumber,
            r.Value,
            r.RecordedAt)).ToList();
    }

    public async Task<ConceptHeaderDto?> GetInstrumentHeaderAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT instrument_id AS Id, code AS Code, name AS Name
            FROM yusay.instrument
            WHERE instrument_id = @InstrumentId;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ConceptHeaderDto>(new CommandDefinition(
            sql,
            new { InstrumentId = instrumentId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ConceptVersionHeaderDto>> GetInstrumentVersionsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT instrument_version_id AS VersionId, version AS VersionNumber
            FROM yusay.instrument_version
            WHERE instrument_id = @InstrumentId
            ORDER BY version ASC, instrument_version_id ASC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ConceptVersionHeaderDto>(new CommandDefinition(
            sql,
            new { InstrumentId = instrumentId },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<IReadOnlyList<CompatibilityPairDto>> GetInstrumentCompatibilityPairsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT version_a_id AS VersionAId, version_b_id AS VersionBId
            FROM yusay.instrument_version_compatibility
            WHERE instrument_id = @InstrumentId;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CompatibilityPairDto>(new CommandDefinition(
            sql,
            new { InstrumentId = instrumentId },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<IReadOnlyList<InstrumentAssessmentPointDto>> GetInstrumentAssessmentResultsAsync(
        Guid userId,
        Guid instrumentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.attempt_id AS AttemptId,
                a.instrument_version_id AS InstrumentVersionId,
                v.version AS VersionNumber,
                r.score AS Score,
                r.calculated_at AS CalculatedAt,
                it.interpretation_id AS InterpretationId,
                it.label AS InterpretationLabel
            FROM yusay.assessment_attempt a
            JOIN yusay.assessment_result r
                ON r.attempt_id = a.attempt_id
            JOIN yusay.instrument_version v
                ON v.instrument_id = a.instrument_id AND v.instrument_version_id = a.instrument_version_id
            JOIN yusay.interpretation it
                ON it.instrument_version_id = a.instrument_version_id AND it.interpretation_id = r.interpretation_id
            WHERE a.user_id = @UserId
              AND a.instrument_id = @InstrumentId
              AND a.status = 'SUBMITTED'
              AND (@From::timestamptz IS NULL OR r.calculated_at >= @From::timestamptz)
              AND (@To::timestamptz IS NULL OR r.calculated_at <= @To::timestamptz)
            ORDER BY r.calculated_at ASC, a.attempt_id ASC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<InstrumentAssessmentRow>(new CommandDefinition(
            sql,
            new
            {
                UserId = userId,
                InstrumentId = instrumentId,
                From = from,
                To = to
            },
            cancellationToken: cancellationToken));

        return rows.Select(r => new InstrumentAssessmentPointDto(
            r.AttemptId,
            r.InstrumentVersionId,
            r.VersionNumber,
            r.Score,
            r.CalculatedAt,
            r.InterpretationId,
            r.InterpretationLabel)).ToList();
    }

    private sealed class DimensionMeasurementRow
    {
        public Guid CheckInId { get; init; }
        public Guid DimensionVersionId { get; init; }
        public int VersionNumber { get; init; }
        public int Value { get; init; }
        public DateTimeOffset RecordedAt { get; init; }
    }

    private sealed class InstrumentAssessmentRow
    {
        public Guid AttemptId { get; init; }
        public Guid InstrumentVersionId { get; init; }
        public int VersionNumber { get; init; }
        public int Score { get; init; }
        public DateTimeOffset CalculatedAt { get; init; }
        public Guid InterpretationId { get; init; }
        public string InterpretationLabel { get; init; } = string.Empty;
    }
}
