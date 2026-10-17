using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Application.Assessment.Queries.ListAdministrativeInstruments;

public interface IListAdministrativeInstrumentsUseCase
{
    Task<IReadOnlyList<InstrumentSummaryDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}

public sealed class ListAdministrativeInstrumentsUseCase(
    ICurrentUser currentUser,
    IInstrumentAdministrationRepository instrumentAdministrationRepository) : IListAdministrativeInstrumentsUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IInstrumentAdministrationRepository _instrumentAdministrationRepository =
        instrumentAdministrationRepository;

    public async Task<IReadOnlyList<InstrumentSummaryDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para consultar el catálogo de Instrumentos.");
        }

        return await _instrumentAdministrationRepository.ListAdministrativeInstrumentsAsync(cancellationToken);
    }
}
