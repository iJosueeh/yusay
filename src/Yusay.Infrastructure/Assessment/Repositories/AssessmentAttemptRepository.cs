using System.Data;
using Dapper;
using Npgsql;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;

namespace Yusay.Infrastructure.Assessment.Repositories;

public sealed class AssessmentAttemptRepository : IAssessmentAttemptRepository
{
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
}
