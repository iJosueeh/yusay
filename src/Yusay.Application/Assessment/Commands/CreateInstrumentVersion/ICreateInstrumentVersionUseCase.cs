using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.CreateInstrumentVersion;

public sealed record CreateInstrumentVersionCommand(
    Guid InstrumentId,
    int? Version,
    string SourceDescription,
    string Population,
    string? AdministrationConditions,
    string? LicenseInformation,
    string Limitations);

public interface ICreateInstrumentVersionUseCase
{
    Task<InstrumentVersionResult> ExecuteAsync(
        CreateInstrumentVersionCommand command,
        CancellationToken cancellationToken = default);
}
