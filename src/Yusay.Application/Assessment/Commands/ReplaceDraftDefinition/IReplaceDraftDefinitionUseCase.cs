using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.ReplaceDraftDefinition;

public sealed record DraftReferenceInput(
    int ReferenceOrder,
    string Citation,
    string? Url);

public sealed record DraftOptionInput(
    int Position,
    string Label,
    int? Contribution);

public sealed record DraftQuestionInput(
    int Position,
    string Prompt,
    bool Required,
    IReadOnlyList<DraftOptionInput> Options);

public sealed record DraftInterpretationInput(
    string Label,
    string Description,
    string? Limitations,
    int LowerBound,
    int UpperBound);

public sealed record ReplaceDraftDefinitionCommand(
    Guid InstrumentId,
    Guid VersionId,
    IReadOnlyList<DraftReferenceInput> References,
    IReadOnlyList<DraftQuestionInput> Questions,
    IReadOnlyList<DraftInterpretationInput> Interpretations);

public interface IReplaceDraftDefinitionUseCase
{
    Task<DraftDefinitionDto> ExecuteAsync(
        ReplaceDraftDefinitionCommand command,
        CancellationToken cancellationToken = default);
}
