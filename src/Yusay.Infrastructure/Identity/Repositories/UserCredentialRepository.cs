using System.Data.Common;
using Dapper;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Domain.Identity.Entities;

namespace Yusay.Infrastructure.Identity.Repositories;

public sealed class UserCredentialRepository(IDbConnectionFactory connectionFactory) : IUserCredentialRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<UserCredential?> GetByUserIdAsync(
        Guid userId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                user_id, 
                password_hash, 
                password_changed_at 
            FROM yusay.user_credential 
            WHERE user_id = @UserId;
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, new { UserId = userId }, transaction, cancellationToken: cancellationToken);
            var row = await conn.QuerySingleOrDefaultAsync<UserCredentialRow>(command);
            return row is null ? null : MapToDomain(row);
        }, cancellationToken);
    }

    public async Task AddAsync(
        UserCredential credential,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.user_credential (
                user_id, 
                password_hash, 
                password_changed_at
            ) VALUES (
                @UserId, 
                @PasswordHash, 
                @PasswordChangedAt
            );
            """;

        var parameters = new
        {
            credential.UserId,
            credential.PasswordHash,
            credential.PasswordChangedAt
        };

        await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
            await conn.ExecuteAsync(command);
        }, cancellationToken);
    }

    public async Task UpdateAsync(
        UserCredential credential,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.user_credential 
            SET 
                password_hash = @PasswordHash, 
                password_changed_at = @PasswordChangedAt 
            WHERE user_id = @UserId;
            """;

        var parameters = new
        {
            credential.UserId,
            credential.PasswordHash,
            credential.PasswordChangedAt
        };

        await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
            await conn.ExecuteAsync(command);
        }, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid userId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM yusay.user_credential WHERE user_id = @UserId;";

        await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, new { UserId = userId }, transaction, cancellationToken: cancellationToken);
            await conn.ExecuteAsync(command);
        }, cancellationToken);
    }

    private static UserCredential MapToDomain(UserCredentialRow row)
    {
        return UserCredential.Rehydrate(
            userId: row.user_id,
            passwordHash: row.password_hash,
            passwordChangedAt: row.password_changed_at);
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

    private sealed class UserCredentialRow
    {
        public Guid user_id { get; init; }
        public string password_hash { get; init; } = string.Empty;
        public DateTimeOffset password_changed_at { get; init; }
    }
}
