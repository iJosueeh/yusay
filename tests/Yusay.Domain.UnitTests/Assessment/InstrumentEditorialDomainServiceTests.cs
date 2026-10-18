using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Models;
using Yusay.Domain.Assessment.Services;
using Yusay.Domain.Common;

namespace Yusay.Domain.UnitTests.Assessment;

public class InstrumentEditorialDomainServiceTests
{
    private static readonly Guid VersionId = Guid.NewGuid();

    private static (List<QuestionScoringComposition> Questions, List<Interpretation> Interpretations) CreateValidComposition(
        int questionCount = 2,
        int optionsPerQuestion = 2,
        bool required = true)
    {
        var questions = new List<QuestionScoringComposition>();
        for (int i = 1; i <= questionCount; i++)
        {
            var q = Question.Create(VersionId, i, $"Pregunta {i}", required);
            var options = new List<AnswerOptionScoringComposition>();
            for (int j = 1; j <= optionsPerQuestion; j++)
            {
                var opt = AnswerOption.Create(q.QuestionId, j, $"Opción {j}");
                options.Add(new AnswerOptionScoringComposition(opt, j - 1));
            }
            questions.Add(new QuestionScoringComposition(q, options));
        }

        // Con 2 preguntas de 2 opciones cada una (contribuciones 0 y 1):
        // Score_min = 0 + 0 = 0
        // Score_max = 1 + 1 = 2
        var interps = new List<Interpretation>
        {
            Interpretation.Create(VersionId, "Bajo", "Puntaje bajo", null, 0, 1),
            Interpretation.Create(VersionId, "Alto", "Puntaje alto", null, 2, 2)
        };

        return (questions, interps);
    }

    [Fact]
    public void EnsurePublishableComposition_WithValidComposition_ShouldNotThrow()
    {
        var (questions, interpretations) = CreateValidComposition();

        InstrumentEditorialDomainService.EnsurePublishableComposition(questions, interpretations);
    }

    [Fact]
    public void EnsurePublishableComposition_WhenQuestionsIsNull_ShouldThrowArgumentNullException()
    {
        var (_, interpretations) = CreateValidComposition();

        Assert.Throws<ArgumentNullException>(() =>
            InstrumentEditorialDomainService.EnsurePublishableComposition(null!, interpretations));
    }

    [Fact]
    public void EnsurePublishableComposition_WhenInterpretationsIsNull_ShouldThrowArgumentNullException()
    {
        var (questions, _) = CreateValidComposition();

        Assert.Throws<ArgumentNullException>(() =>
            InstrumentEditorialDomainService.EnsurePublishableComposition(questions, null!));
    }

    [Fact]
    public void EnsurePublishableComposition_WithZeroQuestions_ShouldThrowDomainException()
    {
        var (_, interpretations) = CreateValidComposition();

        var ex = Assert.Throws<DomainException>(() =>
            InstrumentEditorialDomainService.EnsurePublishableComposition(Array.Empty<QuestionScoringComposition>(), interpretations));

        Assert.Contains("no contiene ninguna pregunta", ex.Message);
    }

    [Fact]
    public void EnsurePublishableComposition_WhenQuestionNotRequired_ShouldThrowDomainExceptionPerRevLog004()
    {
        var (questions, interpretations) = CreateValidComposition(required: false);

        var ex = Assert.Throws<DomainException>(() =>
            InstrumentEditorialDomainService.EnsurePublishableComposition(questions, interpretations));

        Assert.Contains("debe ser obligatoria (required = true)", ex.Message);
        Assert.Contains("REV-LOG-004", ex.Message);
    }

    [Fact]
    public void EnsurePublishableComposition_WhenQuestionHasLessThanTwoOptions_ShouldThrowDomainException()
    {
        var (questions, interpretations) = CreateValidComposition(optionsPerQuestion: 1);

        var ex = Assert.Throws<DomainException>(() =>
            InstrumentEditorialDomainService.EnsurePublishableComposition(questions, interpretations));

        Assert.Contains("menos de 2 opciones de respuesta", ex.Message);
    }

    [Fact]
    public void EnsurePublishableComposition_WhenOptionHasNullContribution_ShouldThrowDomainException()
    {
        var q = Question.Create(VersionId, 1, "Pregunta 1", true);
        var opt1 = AnswerOption.Create(q.QuestionId, 1, "O1");
        var opt2 = AnswerOption.Create(q.QuestionId, 2, "O2");

        var questions = new List<QuestionScoringComposition>
        {
            new(q, new List<AnswerOptionScoringComposition>
            {
                new(opt1, 0),
                new(opt2, null)
            })
        };

        var interps = new List<Interpretation>
        {
            Interpretation.Create(VersionId, "L", "D", null, 0, 0)
        };

        var ex = Assert.Throws<DomainException>(() =>
            InstrumentEditorialDomainService.EnsurePublishableComposition(questions, interps));

        Assert.Contains("carece de contribución de scoring", ex.Message);
    }

    [Fact]
    public void EnsurePublishableComposition_WithZeroInterpretations_ShouldThrowDomainException()
    {
        var (questions, _) = CreateValidComposition();

        var ex = Assert.Throws<DomainException>(() =>
            InstrumentEditorialDomainService.EnsurePublishableComposition(questions, Array.Empty<Interpretation>()));

        Assert.Contains("no tiene interpretaciones registradas", ex.Message);
    }

    [Fact]
    public void EnsurePublishableComposition_WhenInterpretationHasGaps_ShouldThrowDomainException()
    {
        var q = Question.Create(VersionId, 1, "Pregunta 1", true);
        var questions = new List<QuestionScoringComposition>
        {
            new(q, new List<AnswerOptionScoringComposition>
            {
                new(AnswerOption.Create(q.QuestionId, 1, "O1"), 0),
                new(AnswerOption.Create(q.QuestionId, 2, "O2"), 3)
            })
        };

        // Gap entre 1 y 3 (falta el 2)
        var interps = new List<Interpretation>
        {
            Interpretation.Create(VersionId, "Bajo", "D", null, 0, 1),
            Interpretation.Create(VersionId, "Alto", "D", null, 3, 3)
        };

        var ex = Assert.Throws<DomainException>(() =>
            InstrumentEditorialDomainService.EnsurePublishableComposition(questions, interps));

        Assert.Contains("solapamientos o discontinuidades", ex.Message);
    }

    [Fact]
    public void EnsurePublishableComposition_WhenInterpretationCoverageDoesNotMatchMinScore_ShouldThrowDomainException()
    {
        var q = Question.Create(VersionId, 1, "Pregunta 1", true);
        var questions = new List<QuestionScoringComposition>
        {
            new(q, new List<AnswerOptionScoringComposition>
            {
                new(AnswerOption.Create(q.QuestionId, 1, "O1"), 0),
                new(AnswerOption.Create(q.QuestionId, 2, "O2"), 2)
            })
        };

        // El puntaje mínimo posible es 0, pero la interpretación empieza en 1
        var interps = new List<Interpretation>
        {
            Interpretation.Create(VersionId, "Única", "D", null, 1, 2)
        };

        var ex = Assert.Throws<DomainException>(() =>
            InstrumentEditorialDomainService.EnsurePublishableComposition(questions, interps));

        Assert.Contains("no coincide con el rango de puntuaciones posibles", ex.Message);
    }

    [Fact]
    public void EnsurePublishableComposition_WhenInterpretationCoverageDoesNotMatchMaxScore_ShouldThrowDomainException()
    {
        var q = Question.Create(VersionId, 1, "Pregunta 1", true);
        var questions = new List<QuestionScoringComposition>
        {
            new(q, new List<AnswerOptionScoringComposition>
            {
                new(AnswerOption.Create(q.QuestionId, 1, "O1"), 0),
                new(AnswerOption.Create(q.QuestionId, 2, "O2"), 3)
            })
        };

        // El puntaje máximo posible es 3, pero la interpretación termina en 2
        var interps = new List<Interpretation>
        {
            Interpretation.Create(VersionId, "Única", "D", null, 0, 2)
        };

        var ex = Assert.Throws<DomainException>(() =>
            InstrumentEditorialDomainService.EnsurePublishableComposition(questions, interps));

        Assert.Contains("no coincide con el rango de puntuaciones posibles", ex.Message);
    }
}
