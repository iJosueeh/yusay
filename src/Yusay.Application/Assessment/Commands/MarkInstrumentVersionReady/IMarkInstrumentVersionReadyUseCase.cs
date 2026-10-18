using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.MarkInstrumentVersionReady;

public sealed record MarkInstrumentVersionReadyCommand(
    Guid InstrumentId,
    Guid VersionId);

public interface IMarkInstrumentVersionReadyUseCase
{
    Task<InstrumentVersionResult> ExecuteAsync(
        MarkInstrumentVersionReadyCommand command,
        CancellationToken cancellationToken = default);
}
