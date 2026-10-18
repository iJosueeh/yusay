using System.Data;
using Dapper;
using Npgsql;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;

namespace Yusay.Infrastructure.Assessment.Repositories;

public sealed class AssessmentAttemptRepository(IDbConnectionFactory connectionFactory) : IAssessmentAttemptRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task AcquireAttemptAdvisoryLockAsync(
        long lockKey,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT pg_advisory_xact_lock(@LockKey);";

        var connection = transaction.Connection
            ?? throw new InvalidOperationException("La transacción no tiene una conexión asociada.");

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { LockKey = lockKey },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<PublishedVersionHeaderDto?> GetPublishedVersionForShareAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                instrument_version_id AS InstrumentVersionId,
                version AS Version
            FROM yusay.instrument_version
            WHERE instrument_id = @InstrumentId AND status = 'PUBLISHED'
            FOR SHARE;
            """;

        var connection = transaction.Connection
            ?? throw new InvalidOperationException("La transacción no tiene una conexión asociada.");

        var row = await connection.QuerySingleOrDefaultAsync<PublishedVersionHeaderRow>(new CommandDefinition(
            sql,
            new { InstrumentId = instrumentId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        return row is null ? null : new PublishedVersionHeaderDto(row.InstrumentVersionId, row.Version);
    }

    public async Task<ExistingAttemptHeaderDto?> GetActiveAttemptForUpdateAsync(
        Guid userId,
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                attempt_id AS AttemptId,
                started_at AS StartedAt,
                expires_at AS ExpiresAt
            FROM yusay.assessment_attempt
            WHERE user_id = @UserId 
              AND instrument_id = @InstrumentId 
              AND status = 'IN_PROGRESS'
            FOR UPDATE;
            """;

        var connection = transaction.Connection
            ?? throw new InvalidOperationException("La transacción no tiene una conexión asociada.");

        var row = await connection.QuerySingleOrDefaultAsync<ExistingAttemptHeaderRow>(new CommandDefinition(
            sql,
            new { UserId = userId, InstrumentId = instrumentId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        return row is null ? null : new ExistingAttemptHeaderDto(row.AttemptId, row.StartedAt, row.ExpiresAt);
    }

    public async Task ExpireAttemptAsync(
        Guid attemptId,
        DateTimeOffset endedAt,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string deleteAnswersSql = "DELETE FROM yusay.answer WHERE attempt_id = @AttemptId;";
        const string updateAttemptSql = """
            UPDATE yusay.assessment_attempt
            SET status = 'EXPIRED',
                ended_at = @EndedAt
            WHERE attempt_id = @AttemptId;
            """;

        var connection = transaction.Connection
            ?? throw new InvalidOperationException("La transacción no tiene una conexión asociada.");

        await connection.ExecuteAsync(new CommandDefinition(
            deleteAnswersSql,
            new { AttemptId = attemptId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            updateAttemptSql,
            new { AttemptId = attemptId, EndedAt = endedAt },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    public async Task AddAttemptAsync(
        AssessmentAttempt attempt,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.assessment_attempt (
                attempt_id,
                user_id,
                instrument_id,
                instrument_version_id,
                status,
                started_at,
                expires_at,
                ended_at
            ) VALUES (
                @AttemptId,
                @UserId,
                @InstrumentId,
                @InstrumentVersionId,
                @Status,
                @StartedAt,
                @ExpiresAt,
                @EndedAt
            );
            """;

        var connection = transaction.Connection
            ?? throw new InvalidOperationException("La transacción no tiene una conexión asociada.");

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    attempt.AttemptId,
                    attempt.UserId,
                    attempt.InstrumentId,
                    attempt.InstrumentVersionId,
                    Status = attempt.Status.ToDbValue(),
                    attempt.StartedAt,
                    attempt.ExpiresAt,
                    attempt.EndedAt
                },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation &&
            ex.ConstraintName is not null &&
            ex.ConstraintName.Contains("uxp_assessment_attempt_single_in_progress", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(IAssessmentAttemptRepository.ActiveAttemptConflictMessage);
        }
    }

    public async Task<IReadOnlyList<AttemptQuestionDto>> GetAttemptQuestionsAsync(
        Guid instrumentVersionId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                q.question_id AS QuestionId,
                q.position AS QuestionPosition,
                q.prompt AS QuestionPrompt,
                q.required AS QuestionRequired,
                o.option_id AS OptionId,
                o.position AS OptionPosition,
                o.label AS OptionLabel
            FROM yusay.question q
            JOIN yusay.answer_option o ON o.question_id = q.question_id
            WHERE q.instrument_version_id = @InstrumentVersionId
            ORDER BY q.position ASC, o.position ASC;
            """;

        var connection = transaction.Connection
            ?? throw new InvalidOperationException("La transacción no tiene una conexión asociada.");

        var rows = (await connection.QueryAsync<QuestionOptionRow>(new CommandDefinition(
            sql,
            new { InstrumentVersionId = instrumentVersionId },
            transaction: transaction,
            cancellationToken: cancellationToken))).ToList();

        return rows
            .GroupBy(r => new { r.QuestionId, r.QuestionPosition, r.QuestionPrompt, r.QuestionRequired })
            .OrderBy(g => g.Key.QuestionPosition)
            .Select(g => new AttemptQuestionDto(
                g.Key.QuestionId,
                g.Key.QuestionPosition,
                g.Key.QuestionPrompt,
                g.Key.QuestionRequired,
                g.OrderBy(o => o.OptionPosition)
                 .Select(o => new AttemptOptionDto(o.OptionId, o.OptionPosition, o.OptionLabel))
                 .ToList()))
            .ToList();
    }

    public async Task<AttemptDetailsHeaderDto?> GetAttemptHeaderForUpdateAsync(
        Guid attemptId,
        Guid userId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.attempt_id AS AttemptId,
                a.user_id AS UserId,
                a.instrument_id AS InstrumentId,
                a.instrument_version_id AS InstrumentVersionId,
                v.version AS Version,
                a.status AS Status,
                a.started_at AS StartedAt,
                a.expires_at AS ExpiresAt,
                a.ended_at AS EndedAt
            FROM yusay.assessment_attempt a
            JOIN yusay.instrument_version v ON v.instrument_version_id = a.instrument_version_id
            WHERE a.attempt_id = @AttemptId AND a.user_id = @UserId
            FOR UPDATE OF a;
            """;

        var connection = transaction.Connection
            ?? throw new InvalidOperationException("La transacción no tiene una conexión asociada.");

        var row = await connection.QuerySingleOrDefaultAsync<AttemptDetailsHeaderRow>(new CommandDefinition(
            sql,
            new { AttemptId = attemptId, UserId = userId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        return row is null ? null : new AttemptDetailsHeaderDto(
            row.AttemptId,
            row.UserId,
            row.InstrumentId,
            row.InstrumentVersionId,
            row.Version,
            row.Status,
            row.StartedAt,
            row.ExpiresAt,
            row.EndedAt);
    }

    public async Task<AttemptDetailsHeaderDto?> GetAttemptHeaderAsync(
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.attempt_id AS AttemptId,
                a.user_id AS UserId,
                a.instrument_id AS InstrumentId,
                a.instrument_version_id AS InstrumentVersionId,
                v.version AS Version,
                a.status AS Status,
                a.started_at AS StartedAt,
                a.expires_at AS ExpiresAt,
                a.ended_at AS EndedAt
            FROM yusay.assessment_attempt a
            JOIN yusay.instrument_version v ON v.instrument_version_id = a.instrument_version_id
            WHERE a.attempt_id = @AttemptId AND a.user_id = @UserId;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<AttemptDetailsHeaderRow>(new CommandDefinition(
            sql,
            new { AttemptId = attemptId, UserId = userId },
            cancellationToken: cancellationToken));

        return row is null ? null : new AttemptDetailsHeaderDto(
            row.AttemptId,
            row.UserId,
            row.InstrumentId,
            row.InstrumentVersionId,
            row.Version,
            row.Status,
            row.StartedAt,
            row.ExpiresAt,
            row.EndedAt);
    }

    public async Task<IReadOnlyDictionary<Guid, HashSet<Guid>>> GetValidQuestionsAndOptionsAsync(
        Guid instrumentVersionId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                o.question_id AS QuestionId,
                o.option_id AS OptionId
            FROM yusay.answer_option o
            JOIN yusay.question q ON q.question_id = o.question_id
            WHERE q.instrument_version_id = @InstrumentVersionId;
            """;

        var connection = transaction.Connection
            ?? throw new InvalidOperationException("La transacción no tiene una conexión asociada.");

        var rows = await connection.QueryAsync<QuestionOptionPairRow>(new CommandDefinition(
            sql,
            new { InstrumentVersionId = instrumentVersionId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        var dict = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var r in rows)
        {
            if (!dict.TryGetValue(r.QuestionId, out var set))
            {
                set = new HashSet<Guid>();
                dict[r.QuestionId] = set;
            }
            set.Add(r.OptionId);
        }

        return dict;
    }

    public async Task SaveAnswersAsync(
        Guid attemptId,
        Guid instrumentVersionId,
        IReadOnlyList<AssessmentAnswerItemDto> answers,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.answer (
                attempt_id,
                instrument_version_id,
                question_id,
                option_id
            ) VALUES (
                @AttemptId,
                @InstrumentVersionId,
                @QuestionId,
                @OptionId
            )
            ON CONFLICT (attempt_id, question_id)
            DO UPDATE SET option_id = EXCLUDED.option_id;
            """;

        var connection = transaction.Connection
            ?? throw new InvalidOperationException("La transacción no tiene una conexión asociada.");

        var parameters = answers.Select(a => new
        {
            AttemptId = attemptId,
            InstrumentVersionId = instrumentVersionId,
            a.QuestionId,
            a.OptionId
        });

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters,
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AttemptQuestionProgressDto>> GetAttemptProgressQuestionsAsync(
        Guid attemptId,
        Guid instrumentVersionId,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                q.question_id AS QuestionId,
                q.position AS QuestionPosition,
                q.prompt AS QuestionPrompt,
                q.required AS QuestionRequired,
                ans.option_id AS SelectedOptionId,
                o.option_id AS OptionId,
                o.position AS OptionPosition,
                o.label AS OptionLabel
            FROM yusay.question q
            JOIN yusay.answer_option o ON o.question_id = q.question_id
            LEFT JOIN yusay.answer ans ON ans.attempt_id = @AttemptId AND ans.question_id = q.question_id
            WHERE q.instrument_version_id = @InstrumentVersionId
            ORDER BY q.position ASC, o.position ASC;
            """;

        var connection = transaction?.Connection;
        if (connection is not null)
        {
            var rows = (await connection.QueryAsync<QuestionOptionProgressRow>(new CommandDefinition(
                sql,
                new { AttemptId = attemptId, InstrumentVersionId = instrumentVersionId },
                transaction: transaction,
                cancellationToken: cancellationToken))).ToList();

            return MapProgressQuestions(rows);
        }
        else
        {
            await using var newConnection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = (await newConnection.QueryAsync<QuestionOptionProgressRow>(new CommandDefinition(
                sql,
                new { AttemptId = attemptId, InstrumentVersionId = instrumentVersionId },
                cancellationToken: cancellationToken))).ToList();

            return MapProgressQuestions(rows);
        }
    }

    public async Task ExpireAttemptCommittedAsync(
        Guid attemptId,
        DateTimeOffset endedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string deleteAnswersSql = "DELETE FROM yusay.answer WHERE attempt_id = @AttemptId;";
        const string updateAttemptSql = """
            UPDATE yusay.assessment_attempt
            SET status = 'EXPIRED',
                ended_at = @EndedAt
            WHERE attempt_id = @AttemptId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            deleteAnswersSql,
            new { AttemptId = attemptId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            updateAttemptSql,
            new { AttemptId = attemptId, EndedAt = endedAt },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    private static IReadOnlyList<AttemptQuestionProgressDto> MapProgressQuestions(List<QuestionOptionProgressRow> rows)
    {
        return rows
            .GroupBy(r => new { r.QuestionId, r.QuestionPosition, r.QuestionPrompt, r.QuestionRequired, r.SelectedOptionId })
            .OrderBy(g => g.Key.QuestionPosition)
            .Select(g => new AttemptQuestionProgressDto(
                g.Key.QuestionId,
                g.Key.QuestionPosition,
                g.Key.QuestionPrompt,
                g.Key.QuestionRequired,
                g.Key.SelectedOptionId,
                g.OrderBy(o => o.OptionPosition)
                 .Select(o => new AttemptOptionDto(o.OptionId, o.OptionPosition, o.OptionLabel))
                 .ToList()))
            .ToList();
    }

    private sealed class QuestionOptionRow
    {
        public Guid QuestionId { get; init; }
        public int QuestionPosition { get; init; }
        public string QuestionPrompt { get; init; } = string.Empty;
        public bool QuestionRequired { get; init; }
        public Guid OptionId { get; init; }
        public int OptionPosition { get; init; }
        public string OptionLabel { get; init; } = string.Empty;
    }

    private sealed class PublishedVersionHeaderRow
    {
        public Guid InstrumentVersionId { get; init; }
        public int Version { get; init; }
    }

    private sealed class ExistingAttemptHeaderRow
    {
        public Guid AttemptId { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }

    private sealed class AttemptDetailsHeaderRow
    {
        public Guid AttemptId { get; init; }
        public Guid UserId { get; init; }
        public Guid InstrumentId { get; init; }
        public Guid InstrumentVersionId { get; init; }
        public int Version { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset? EndedAt { get; init; }
    }

    private sealed class QuestionOptionPairRow
    {
        public Guid QuestionId { get; init; }
        public Guid OptionId { get; init; }
    }

    private sealed class QuestionOptionProgressRow
    {
        public Guid QuestionId { get; init; }
        public int QuestionPosition { get; init; }
        public string QuestionPrompt { get; init; } = string.Empty;
        public bool QuestionRequired { get; init; }
        public Guid? SelectedOptionId { get; init; }
        public Guid OptionId { get; init; }
        public int OptionPosition { get; init; }
        public string OptionLabel { get; init; } = string.Empty;
    }
}
