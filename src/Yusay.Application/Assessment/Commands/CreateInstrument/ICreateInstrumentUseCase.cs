using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.CreateInstrument;

public sealed record CreateInstrumentCommand(
    string Code,
    string Name,
    string Description,
    string Purpose);

public interface ICreateInstrumentUseCase
{
    Task<InstrumentResult> ExecuteAsync(
        CreateInstrumentCommand command,
        CancellationToken cancellationToken = default);
}
