using System.Data.Common;
using Dapper;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Domain.Identity.Entities;

namespace Yusay.Infrastructure.Identity.Repositories;

public sealed class EmailVerificationTokenRepository(IDbConnectionFactory connectionFactory) : IEmailVerificationTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<EmailVerificationToken?> GetByHashAsync(
        string tokenHash,
        DbTransaction? transaction = null,
        bool forUpdate = false,
        CancellationToken cancellationToken = default)
    {
        var sql = forUpdate
            ? """
              SELECT 
                  verification_token_id, 
                  user_id, 
                  token_hash, 
                  created_at, 
                  expires_at 
              FROM yusay.email_verification_token 
              WHERE token_hash = @TokenHash
              FOR UPDATE;
              """
            : """
              SELECT 
                  verification_token_id, 
                  user_id, 
                  token_hash, 
                  created_at, 
                  expires_at 
              FROM yusay.email_verification_token 
              WHERE token_hash = @TokenHash;
              """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, new { TokenHash = tokenHash }, transaction, cancellationToken: cancellationToken);
            var row = await conn.QuerySingleOrDefaultAsync<EmailVerificationTokenRow>(command);
            return row is null ? null : MapToDomain(row);
        }, cancellationToken);
    }

    public async Task<EmailVerificationToken?> GetLatestByUserIdAsync(
        Guid userId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                verification_token_id, 
                user_id, 
                token_hash, 
                created_at, 
                expires_at 
            FROM yusay.email_verification_token 
            WHERE user_id = @UserId 
            ORDER BY created_at DESC 
            LIMIT 1;
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, new { UserId = userId }, transaction, cancellationToken: cancellationToken);
            var row = await conn.QuerySingleOrDefaultAsync<EmailVerificationTokenRow>(command);
            return row is null ? null : MapToDomain(row);
        }, cancellationToken);
    }

    public async Task AddAsync(
        EmailVerificationToken token,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.email_verification_token (
                verification_token_id, 
                user_id, 
                token_hash, 
                created_at, 
                expires_at
            ) VALUES (
                @VerificationTokenId, 
                @UserId, 
                @TokenHash, 
                @CreatedAt, 
                @ExpiresAt
            );
            """;

        var parameters = new
        {
            token.VerificationTokenId,
            token.UserId,
            token.TokenHash,
            token.CreatedAt,
            token.ExpiresAt
        };

        await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
            await conn.ExecuteAsync(command);
        }, cancellationToken);
    }

    public async Task InvalidateAllForUserAsync(
        Guid userId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM yusay.email_verification_token WHERE user_id = @UserId;";

        await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, new { UserId = userId }, transaction, cancellationToken: cancellationToken);
            await conn.ExecuteAsync(command);
        }, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid verificationTokenId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM yusay.email_verification_token WHERE verification_token_id = @VerificationTokenId;";

        await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, new { VerificationTokenId = verificationTokenId }, transaction, cancellationToken: cancellationToken);
            await conn.ExecuteAsync(command);
        }, cancellationToken);
    }

    private static EmailVerificationToken MapToDomain(EmailVerificationTokenRow row)
    {
        return EmailVerificationToken.Rehydrate(
            verificationTokenId: row.verification_token_id,
            userId: row.user_id,
            tokenHash: row.token_hash,
            createdAt: row.created_at,
            expiresAt: row.expires_at);
    }

    private async Task<T> ExecuteWithConnectionAsync<T>(
        DbTransaction? transaction,
        Func<DbConnection, Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (transaction?.Connection != null)
        {
            return await action(transaction.Connection);
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await action(connection);
    }

    private async Task ExecuteWithConnectionAsync(
        DbTransaction? transaction,
        Func<DbConnection, Task> action,
        CancellationToken cancellationToken)
    {
        if (transaction?.Connection != null)
        {
            await action(transaction.Connection);
            return;
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await action(connection);
    }

    private sealed class EmailVerificationTokenRow
    {
        public Guid verification_token_id { get; init; }
        public Guid user_id { get; init; }
        public string token_hash { get; init; } = string.Empty;
        public DateTimeOffset created_at { get; init; }
        public DateTimeOffset expires_at { get; init; }
    }
}
