using System.Data;
using System.Data.Common;
using Dapper;
using Npgsql;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Assessment.ValueObjects;

namespace Yusay.Infrastructure.Assessment.Repositories;

public sealed class InstrumentAdministrationRepository(IDbConnectionFactory connectionFactory)
    : IInstrumentAdministrationRepository
{
    private const string DuplicateCodeConstraintName = "uq_instrument_code";
    private const string DuplicateVersionNumberConstraintName = "uq_instrument_version_instrument_id_version";

    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM yusay.instrument WHERE code = @Code);";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql, new { Code = code }, cancellationToken: cancellationToken));
    }

    public async Task<Instrument?> GetByIdAsync(Guid instrumentId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT instrument_id, code, name, description, purpose
            FROM yusay.instrument
            WHERE instrument_id = @InstrumentId;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<InstrumentRow>(
            new CommandDefinition(sql, new { InstrumentId = instrumentId }, cancellationToken: cancellationToken));

        return row is null ? null : MapInstrument(row);
    }

    public async Task<Instrument?> GetByIdForUpdateAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT instrument_id, code, name, description, purpose
            FROM yusay.instrument
            WHERE instrument_id = @InstrumentId
            FOR UPDATE;
            """;

        var dbTx = (DbTransaction)transaction;
        var row = await dbTx.Connection!.QuerySingleOrDefaultAsync<InstrumentRow>(
            new CommandDefinition(sql, new { InstrumentId = instrumentId }, dbTx, cancellationToken: cancellationToken));

        return row is null ? null : MapInstrument(row);
    }

    public async Task AddInstrumentAsync(
        Instrument instrument,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.instrument (instrument_id, code, name, description, purpose)
            VALUES (@InstrumentId, @Code, @Name, @Description, @Purpose);
            """;

        var parameters = new
        {
            instrument.InstrumentId,
            Code = instrument.Code.Value,
            instrument.Name,
            instrument.Description,
            instrument.Purpose
        };

        var dbTx = (DbTransaction)transaction;
        try
        {
            await dbTx.Connection!.ExecuteAsync(
                new CommandDefinition(sql, parameters, dbTx, cancellationToken: cancellationToken));
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UniqueViolation &&
            string.Equals(ex.ConstraintName, DuplicateCodeConstraintName, StringComparison.Ordinal))
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateCodeConflictMessage);
        }
    }

    public async Task UpdateInstrumentAsync(
        Instrument instrument,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.instrument
            SET name = @Name,
                description = @Description,
                purpose = @Purpose
            WHERE instrument_id = @InstrumentId;
            """;

        var parameters = new
        {
            instrument.InstrumentId,
            instrument.Name,
            instrument.Description,
            instrument.Purpose
        };

        var dbTx = (DbTransaction)transaction;
        await dbTx.Connection!.ExecuteAsync(
            new CommandDefinition(sql, parameters, dbTx, cancellationToken: cancellationToken));
    }

    public async Task<bool> VersionNumberExistsAsync(
        Guid instrumentId,
        int version,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM yusay.instrument_version
                WHERE instrument_id = @InstrumentId AND version = @Version
            );
            """;

        if (transaction != null)
        {
            var dbTx = (DbTransaction)transaction;
            return await dbTx.Connection!.ExecuteScalarAsync<bool>(
                new CommandDefinition(sql, new { InstrumentId = instrumentId, Version = version }, dbTx, cancellationToken: cancellationToken));
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { InstrumentId = instrumentId, Version = version }, cancellationToken: cancellationToken));
    }

    public async Task<int> GetNextVersionNumberAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COALESCE(MAX(version), 0) + 1
            FROM yusay.instrument_version
            WHERE instrument_id = @InstrumentId;
            """;

        var dbTx = (DbTransaction)transaction;
        return await dbTx.Connection!.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { InstrumentId = instrumentId }, dbTx, cancellationToken: cancellationToken));
    }

    public async Task AddVersionAsync(
        InstrumentVersion version,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string insertVersionSql = """
            INSERT INTO yusay.instrument_version (
                instrument_version_id,
                instrument_id,
                version,
                status,
                source_description,
                population,
                administration_conditions,
                license_information,
                limitations
            ) VALUES (
                @InstrumentVersionId,
                @InstrumentId,
                @Version,
                @Status,
                @SourceDescription,
                @Population,
                @AdministrationConditions,
                @LicenseInformation,
                @Limitations
            );
            """;

        const string insertScoringDefSql = """
            INSERT INTO yusay.scoring_definition (instrument_version_id, method)
            VALUES (@InstrumentVersionId, 'SUM');
            """;

        var parameters = new
        {
            version.InstrumentVersionId,
            version.InstrumentId,
            version.Version,
            Status = version.Status.ToDbValue(),
            version.SourceDescription,
            version.Population,
            version.AdministrationConditions,
            version.LicenseInformation,
            version.Limitations
        };

        var dbTx = (DbTransaction)transaction;
        try
        {
            await dbTx.Connection!.ExecuteAsync(
                new CommandDefinition(insertVersionSql, parameters, dbTx, cancellationToken: cancellationToken));

            await dbTx.Connection!.ExecuteAsync(
                new CommandDefinition(insertScoringDefSql, new { version.InstrumentVersionId }, dbTx, cancellationToken: cancellationToken));
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UniqueViolation &&
            string.Equals(ex.ConstraintName, DuplicateVersionNumberConstraintName, StringComparison.Ordinal))
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateVersionConflictMessage);
        }
    }

    public async Task<InstrumentVersion?> GetVersionByIdAsync(
        Guid instrumentVersionId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                instrument_version_id,
                instrument_id,
                version,
                status,
                source_description,
                population,
                administration_conditions,
                license_information,
                limitations
            FROM yusay.instrument_version
            WHERE instrument_version_id = @InstrumentVersionId;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<InstrumentVersionRow>(
            new CommandDefinition(sql, new { InstrumentVersionId = instrumentVersionId }, cancellationToken: cancellationToken));

        return row is null ? null : MapInstrumentVersion(row);
    }

    public async Task<IReadOnlyList<InstrumentSummaryDto>> ListAdministrativeInstrumentsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                i.instrument_id AS InstrumentId,
                i.code AS Code,
                i.name AS Name,
                i.description AS Description,
                i.purpose AS Purpose,
                COUNT(iv.instrument_version_id)::int AS TotalVersions,
                MAX(CASE WHEN iv.status = 'PUBLISHED' THEN iv.version ELSE NULL END) AS PublishedVersion
            FROM yusay.instrument i
            LEFT JOIN yusay.instrument_version iv ON iv.instrument_id = i.instrument_id
            GROUP BY i.instrument_id, i.code, i.name, i.description, i.purpose
            ORDER BY i.name ASC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<InstrumentSummaryDto>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return items.ToList();
    }

    public async Task<InstrumentDetailsDto?> GetAdministrativeInstrumentDetailsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        const string instrumentSql = """
            SELECT instrument_id, code, name, description, purpose
            FROM yusay.instrument
            WHERE instrument_id = @InstrumentId;
            """;

        const string versionsSql = """
            SELECT
                instrument_version_id AS InstrumentVersionId,
                version AS Version,
                status AS Status,
                source_description AS SourceDescription,
                population AS Population,
                administration_conditions AS AdministrationConditions,
                license_information AS LicenseInformation,
                limitations AS Limitations
            FROM yusay.instrument_version
            WHERE instrument_id = @InstrumentId
            ORDER BY version DESC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var instrumentRow = await connection.QuerySingleOrDefaultAsync<InstrumentRow>(
            new CommandDefinition(instrumentSql, new { InstrumentId = instrumentId }, cancellationToken: cancellationToken));

        if (instrumentRow is null)
            return null;

        var versions = (await connection.QueryAsync<InstrumentVersionSummaryDto>(
            new CommandDefinition(versionsSql, new { InstrumentId = instrumentId }, cancellationToken: cancellationToken))).ToList();

        return new InstrumentDetailsDto(
            instrumentRow.Instrument_id,
            instrumentRow.Code,
            instrumentRow.Name,
            instrumentRow.Description,
            instrumentRow.Purpose,
            versions);
    }

    public async Task<InstrumentVersion?> GetVersionByIdForUpdateAsync(
        Guid instrumentVersionId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT instrument_version_id, instrument_id, version, status,
                   source_description, population, administration_conditions,
                   license_information, limitations
            FROM yusay.instrument_version
            WHERE instrument_version_id = @VersionId
            FOR UPDATE;
            """;

        var row = await transaction.Connection!.QuerySingleOrDefaultAsync<InstrumentVersionRow>(
            new CommandDefinition(
                sql,
                new { VersionId = instrumentVersionId },
                transaction: transaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapInstrumentVersion(row);
    }

    public async Task<DraftDefinitionDto?> GetDraftDefinitionAsync(
        Guid instrumentVersionId,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var connection = transaction?.Connection ?? await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string versionSql = """
            SELECT instrument_version_id, instrument_id, version, status
            FROM yusay.instrument_version
            WHERE instrument_version_id = @VersionId;
            """;

        var vRow = await connection.QuerySingleOrDefaultAsync<VersionStatusRow>(
            new CommandDefinition(versionSql, new { VersionId = instrumentVersionId }, transaction: transaction, cancellationToken: cancellationToken));

        if (vRow is null)
            return null;

        const string refSql = """
            SELECT reference_order AS ReferenceOrder, citation AS Citation, url AS Url
            FROM yusay.instrument_version_reference
            WHERE instrument_version_id = @VersionId
            ORDER BY reference_order ASC;
            """;

        var refs = (await connection.QueryAsync<DraftReferenceDto>(
            new CommandDefinition(refSql, new { VersionId = instrumentVersionId }, transaction: transaction, cancellationToken: cancellationToken))).ToList();

        const string qSql = """
            SELECT question_id, position, prompt, required
            FROM yusay.question
            WHERE instrument_version_id = @VersionId
            ORDER BY position ASC;
            """;

        var rawQuestions = (await connection.QueryAsync<QuestionRow>(
            new CommandDefinition(qSql, new { VersionId = instrumentVersionId }, transaction: transaction, cancellationToken: cancellationToken))).ToList();

        const string optSql = """
            SELECT ao.option_id, ao.question_id, ao.position, ao.label, sc.contribution
            FROM yusay.answer_option ao
            JOIN yusay.question q ON q.question_id = ao.question_id
            LEFT JOIN yusay.scoring_contribution sc ON sc.option_id = ao.option_id
            WHERE q.instrument_version_id = @VersionId
            ORDER BY ao.question_id, ao.position ASC;
            """;

        var rawOptions = (await connection.QueryAsync<OptionRow>(
            new CommandDefinition(optSql, new { VersionId = instrumentVersionId }, transaction: transaction, cancellationToken: cancellationToken))).ToList();

        var optionsByQuestion = rawOptions
            .GroupBy(o => o.Question_id)
            .ToDictionary(
                g => g.Key,
                g => g.Select(o => new DraftOptionDto(o.Option_id, o.Position, o.Label, o.Contribution)).ToList());

        var questions = rawQuestions.Select(q =>
        {
            var opts = optionsByQuestion.TryGetValue(q.Question_id, out var oList) ? oList : new List<DraftOptionDto>();
            return new DraftQuestionDto(q.Question_id, q.Position, q.Prompt, q.Required, opts);
        }).ToList();

        const string interpSql = """
            SELECT interpretation_id AS InterpretationId, label AS Label, description AS Description,
                   limitations AS Limitations, lower_bound AS LowerBound, upper_bound AS UpperBound
            FROM yusay.interpretation
            WHERE instrument_version_id = @VersionId
            ORDER BY lower_bound ASC, upper_bound ASC;
            """;

        var interps = (await connection.QueryAsync<DraftInterpretationDto>(
            new CommandDefinition(interpSql, new { VersionId = instrumentVersionId }, transaction: transaction, cancellationToken: cancellationToken))).ToList();

        return new DraftDefinitionDto(
            vRow.Instrument_version_id,
            vRow.Instrument_id,
            vRow.Version,
            vRow.Status,
            refs,
            questions,
            interps);
    }

    public async Task ReplaceDraftDefinitionAsync(
        Guid instrumentVersionId,
        IReadOnlyList<InstrumentVersionReference> references,
        IReadOnlyList<(Question Question, IReadOnlyList<(AnswerOption Option, int? Contribution)> Options)> questionsWithChildren,
        IReadOnlyList<Interpretation> interpretations,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var conn = transaction.Connection!;

        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM yusay.scoring_contribution WHERE instrument_version_id = @VersionId",
            new { VersionId = instrumentVersionId }, transaction: transaction, cancellationToken: cancellationToken));

        await conn.ExecuteAsync(new CommandDefinition("""
            DELETE FROM yusay.answer_option
            WHERE question_id IN (SELECT question_id FROM yusay.question WHERE instrument_version_id = @VersionId)
            """,
            new { VersionId = instrumentVersionId }, transaction: transaction, cancellationToken: cancellationToken));

        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM yusay.question WHERE instrument_version_id = @VersionId",
            new { VersionId = instrumentVersionId }, transaction: transaction, cancellationToken: cancellationToken));

        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM yusay.interpretation WHERE instrument_version_id = @VersionId",
            new { VersionId = instrumentVersionId }, transaction: transaction, cancellationToken: cancellationToken));

        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM yusay.instrument_version_reference WHERE instrument_version_id = @VersionId",
            new { VersionId = instrumentVersionId }, transaction: transaction, cancellationToken: cancellationToken));

        if (references.Count > 0)
        {
            const string insertRefSql = """
                INSERT INTO yusay.instrument_version_reference (instrument_version_id, reference_order, citation, url)
                VALUES (@InstrumentVersionId, @ReferenceOrder, @Citation, @Url);
                """;
            var refParams = references.Select(r => new
            {
                r.InstrumentVersionId,
                r.ReferenceOrder,
                r.Citation,
                r.Url
            });
            await conn.ExecuteAsync(new CommandDefinition(insertRefSql, refParams, transaction: transaction, cancellationToken: cancellationToken));
        }

        if (questionsWithChildren.Count > 0)
        {
            const string insertQuestionSql = """
                INSERT INTO yusay.question (question_id, instrument_version_id, position, prompt, required)
                VALUES (@QuestionId, @InstrumentVersionId, @Position, @Prompt, @Required);
                """;

            const string insertOptionSql = """
                INSERT INTO yusay.answer_option (option_id, question_id, position, label)
                VALUES (@OptionId, @QuestionId, @Position, @Label);
                """;

            const string insertContribSql = """
                INSERT INTO yusay.scoring_contribution (option_id, question_id, instrument_version_id, contribution)
                VALUES (@OptionId, @QuestionId, @InstrumentVersionId, @Contribution);
                """;

            foreach (var (question, options) in questionsWithChildren)
            {
                await conn.ExecuteAsync(new CommandDefinition(
                    insertQuestionSql,
                    new
                    {
                        question.QuestionId,
                        question.InstrumentVersionId,
                        question.Position,
                        question.Prompt,
                        question.Required
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

                foreach (var (option, contribution) in options)
                {
                    await conn.ExecuteAsync(new CommandDefinition(
                        insertOptionSql,
                        new
                        {
                            option.OptionId,
                            option.QuestionId,
                            option.Position,
                            option.Label
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                    if (contribution.HasValue)
                    {
                        await conn.ExecuteAsync(new CommandDefinition(
                            insertContribSql,
                            new
                            {
                                OptionId = option.OptionId,
                                QuestionId = question.QuestionId,
                                InstrumentVersionId = instrumentVersionId,
                                Contribution = contribution.Value
                            },
                            transaction: transaction,
                            cancellationToken: cancellationToken));
                    }
                }
            }
        }

        if (interpretations.Count > 0)
        {
            const string insertInterpSql = """
                INSERT INTO yusay.interpretation (interpretation_id, instrument_version_id, label, description, limitations, lower_bound, upper_bound)
                VALUES (@InterpretationId, @InstrumentVersionId, @Label, @Description, @Limitations, @LowerBound, @UpperBound);
                """;

            var interpParams = interpretations.Select(i => new
            {
                i.InterpretationId,
                i.InstrumentVersionId,
                i.Label,
                i.Description,
                i.Limitations,
                i.LowerBound,
                i.UpperBound
            });

            await conn.ExecuteAsync(new CommandDefinition(insertInterpSql, interpParams, transaction: transaction, cancellationToken: cancellationToken));
        }
    }


    private static Instrument MapInstrument(InstrumentRow row) =>
        Instrument.Rehydrate(
            row.Instrument_id,
            InstrumentCode.Create(row.Code),
            row.Name,
            row.Description,
            row.Purpose);

    private static InstrumentVersion MapInstrumentVersion(InstrumentVersionRow row) =>
        InstrumentVersion.Rehydrate(
            row.Instrument_version_id,
            row.Instrument_id,
            row.Version,
            InstrumentVersionStatusExtensions.FromDbValue(row.Status),
            row.Source_description,
            row.Population,
            row.Administration_conditions,
            row.License_information,
            row.Limitations);

    private sealed class InstrumentRow
    {
        public Guid Instrument_id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
    }

    private sealed class InstrumentVersionRow
    {
        public Guid Instrument_version_id { get; set; }
        public Guid Instrument_id { get; set; }
        public int Version { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Source_description { get; set; } = string.Empty;
        public string Population { get; set; } = string.Empty;
        public string? Administration_conditions { get; set; }
        public string? License_information { get; set; }
        public string Limitations { get; set; } = string.Empty;
    }

    private sealed class VersionStatusRow
    {
        public Guid Instrument_version_id { get; set; }
        public Guid Instrument_id { get; set; }
        public int Version { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private sealed class QuestionRow
    {
        public Guid Question_id { get; set; }
        public int Position { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public bool Required { get; set; }
    }

    private sealed class OptionRow
    {
        public Guid Option_id { get; set; }
        public Guid Question_id { get; set; }
        public int Position { get; set; }
        public string Label { get; set; } = string.Empty;
        public int? Contribution { get; set; }
    }
}
