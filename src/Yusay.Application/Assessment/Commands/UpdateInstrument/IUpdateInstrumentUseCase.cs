using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.UpdateInstrument;

public sealed record UpdateInstrumentCommand(
    Guid InstrumentId,
    string? Name,
    string? Description,
    string? Purpose);

public interface IUpdateInstrumentUseCase
{
    Task<InstrumentResult> ExecuteAsync(
        UpdateInstrumentCommand command,
        CancellationToken cancellationToken = default);
}
