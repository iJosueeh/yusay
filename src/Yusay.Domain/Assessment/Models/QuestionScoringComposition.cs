using Yusay.Domain.Assessment.Entities;

namespace Yusay.Domain.Assessment.Models;

public sealed class QuestionScoringComposition
{
    public Question Question { get; }
    public IReadOnlyList<AnswerOptionScoringComposition> Options { get; }

    public QuestionScoringComposition(
        Question question,
        IReadOnlyList<AnswerOptionScoringComposition> options)
    {
        Question = question ?? throw new ArgumentNullException(nameof(question));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }
}

public sealed class AnswerOptionScoringComposition
{
    public AnswerOption Option { get; }
    public int? Contribution { get; }

    public AnswerOptionScoringComposition(
        AnswerOption option,
        int? contribution)
    {
        Option = option ?? throw new ArgumentNullException(nameof(option));
        Contribution = contribution;
    }
}
