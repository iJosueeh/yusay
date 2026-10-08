using System.Data.Common;
using Dapper;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.Enums;
using Yusay.Domain.Identity.ValueObjects;

namespace Yusay.Infrastructure.Identity.Repositories;

public sealed class UserAccountRepository(IDbConnectionFactory connectionFactory) : IUserAccountRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<UserAccount?> GetByIdAsync(
        Guid id,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                user_id, 
                email, 
                email_verified_at, 
                created_at, 
                adult_confirmed_at, 
                status 
            FROM yusay.user_account 
            WHERE user_id = @Id;
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, new { Id = id }, transaction, cancellationToken: cancellationToken);
            var row = await conn.QuerySingleOrDefaultAsync<UserAccountRow>(command);
            return row is null ? null : MapToDomain(row);
        }, cancellationToken);
    }

    public async Task<UserAccount?> GetByEmailAsync(
        Email email,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                user_id, 
                email, 
                email_verified_at, 
                created_at, 
                adult_confirmed_at, 
                status 
            FROM yusay.user_account 
            WHERE lower(email) = lower(@Email);
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, new { Email = email.Value }, transaction, cancellationToken: cancellationToken);
            var row = await conn.QuerySingleOrDefaultAsync<UserAccountRow>(command);
            return row is null ? null : MapToDomain(row);
        }, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(
        Email email,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 
                FROM yusay.user_account 
                WHERE lower(email) = lower(@Email)
            );
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, new { Email = email.Value }, transaction, cancellationToken: cancellationToken);
            return await conn.ExecuteScalarAsync<bool>(command);
        }, cancellationToken);
    }

    public async Task AddAsync(
        UserAccount userAccount,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.user_account (
                user_id, 
                email, 
                email_verified_at, 
                created_at, 
                adult_confirmed_at, 
                status
            ) VALUES (
                @Id, 
                @Email, 
                @EmailVerifiedAt, 
                @CreatedAt, 
                @AdultConfirmedAt, 
                @Status
            );
            """;

        var parameters = new
        {
            userAccount.Id,
            Email = userAccount.Email.Value,
            userAccount.EmailVerifiedAt,
            userAccount.CreatedAt,
            userAccount.AdultConfirmedAt,
            Status = userAccount.Status.ToString().ToUpperInvariant()
        };

        await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
            await conn.ExecuteAsync(command);
        }, cancellationToken);
    }

    public async Task UpdateAsync(
        UserAccount userAccount,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.user_account 
            SET 
                email_verified_at = @EmailVerifiedAt,
                status = @Status
            WHERE user_id = @Id;
            """;

        var parameters = new
        {
            userAccount.Id,
            userAccount.EmailVerifiedAt,
            Status = userAccount.Status.ToString().ToUpperInvariant()
        };

        await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
            await conn.ExecuteAsync(command);
        }, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM yusay.user_account WHERE user_id = @Id;";

        await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, new { Id = id }, transaction, cancellationToken: cancellationToken);
            await conn.ExecuteAsync(command);
        }, cancellationToken);
    }

    private static UserAccount MapToDomain(UserAccountRow row)
    {
        return UserAccount.Rehydrate(
            id: row.user_id,
            email: Email.Create(row.email),
            emailVerifiedAt: row.email_verified_at,
            createdAt: row.created_at,
            adultConfirmedAt: row.adult_confirmed_at,
            status: Enum.Parse<UserAccountStatus>(row.status, ignoreCase: true));
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

    private sealed class UserAccountRow
    {
        public Guid user_id { get; init; }
        public string email { get; init; } = string.Empty;
        public DateTimeOffset? email_verified_at { get; init; }
        public DateTimeOffset created_at { get; init; }
        public DateTimeOffset adult_confirmed_at { get; init; }
        public string status { get; init; } = string.Empty;
    }
}
