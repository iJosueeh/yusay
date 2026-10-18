using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.ReturnInstrumentVersionToDraft;

public sealed record ReturnInstrumentVersionToDraftCommand(
    Guid InstrumentId,
    Guid VersionId);

public interface IReturnInstrumentVersionToDraftUseCase
{
    Task<InstrumentVersionResult> ExecuteAsync(
        ReturnInstrumentVersionToDraftCommand command,
        CancellationToken cancellationToken = default);
}
