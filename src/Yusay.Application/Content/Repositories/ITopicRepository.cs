using System.Data.Common;
using Yusay.Domain.Content.Entities;

namespace Yusay.Application.Content.Repositories;

public interface ITopicRepository
{
    const string DuplicateCodeConflictMessage = "Ya existe un tema registrado con este código.";

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    Task<Topic?> GetByIdAsync(Guid topicId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Topic>> ListAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Topic topic, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    Task<bool> AllTopicsExistAsync(IEnumerable<Guid> topicIds, CancellationToken cancellationToken = default);
}
