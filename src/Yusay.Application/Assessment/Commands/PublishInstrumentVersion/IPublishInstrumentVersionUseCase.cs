using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.PublishInstrumentVersion;

public sealed record PublishInstrumentVersionCommand(
    Guid InstrumentId,
    Guid VersionId);

public interface IPublishInstrumentVersionUseCase
{
    Task<InstrumentVersionResult> ExecuteAsync(
        PublishInstrumentVersionCommand command,
        CancellationToken cancellationToken = default);
}
