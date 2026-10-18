using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.RetireInstrumentVersion;

public sealed record RetireInstrumentVersionCommand(
    Guid InstrumentId,
    Guid VersionId);

public interface IRetireInstrumentVersionUseCase
{
    Task<InstrumentVersionResult> ExecuteAsync(
        RetireInstrumentVersionCommand command,
        CancellationToken cancellationToken = default);
}
