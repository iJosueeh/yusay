using System.Data.Common;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Common.Interfaces;

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}
