using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Models;
using Yusay.Domain.Common;

namespace Yusay.Domain.Assessment.Services;

public static class InstrumentEditorialDomainService
{
    public static void EnsurePublishableComposition(
        IReadOnlyList<QuestionScoringComposition> questions,
        IReadOnlyList<Interpretation> interpretations)
    {
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(interpretations);

        // 1. Al menos 1 pregunta (V016, requisito 2)
        if (questions.Count == 0)
        {
            throw new DomainException("No se puede marcar como READY la versión: no contiene ninguna pregunta.");
        }

        // 2. Obligatoriedad de preguntas (REV-LOG-004) y estructura de opciones (V016, requisitos 3 y 4)
        foreach (var qComp in questions)
        {
            var q = qComp.Question;
            if (!q.Required)
            {
                throw new DomainException(
                    $"No se puede marcar como READY la versión: la pregunta en posición {q.Position} debe ser obligatoria (required = true) según REV-LOG-004.");
            }

            if (qComp.Options.Count < 2)
            {
                throw new DomainException(
                    $"No se puede marcar como READY la versión: la pregunta en posición {q.Position} tiene menos de 2 opciones de respuesta.");
            }

            foreach (var optComp in qComp.Options)
            {
                if (!optComp.Contribution.HasValue)
                {
                    throw new DomainException(
                        $"No se puede marcar como READY la versión: la opción en posición {optComp.Option.Position} de la pregunta {q.Position} carece de contribución de scoring.");
                }
            }
        }

        // 3. Cálculo exacto de límites de puntuación [Score_min, Score_max]
        // Reproduce idénticamente la matemática de V016:
        // Score_min = SUM(CASE WHEN required THEN opt_min ELSE LEAST(0, opt_min) END)
        // Score_max = SUM(CASE WHEN required THEN opt_max ELSE GREATEST(0, opt_max) END)
        int minScore = 0;
        int maxScore = 0;

        foreach (var qComp in questions)
        {
            int optMin = qComp.Options.Min(o => o.Contribution!.Value);
            int optMax = qComp.Options.Max(o => o.Contribution!.Value);

            minScore += qComp.Question.Required ? optMin : Math.Min(0, optMin);
            maxScore += qComp.Question.Required ? optMax : Math.Max(0, optMax);
        }

        // 4. Validar interpretaciones (cobertura continua exacta conforme a V016, requisito 6)
        if (interpretations.Count == 0)
        {
            throw new DomainException("No se puede marcar como READY la versión: no tiene interpretaciones registradas.");
        }

        var sortedInterps = interpretations
            .OrderBy(i => i.LowerBound)
            .ThenBy(i => i.UpperBound)
            .ToList();

        // Intervalos válidos
        foreach (var interp in sortedInterps)
        {
            if (interp.LowerBound > interp.UpperBound)
            {
                throw new DomainException(
                    $"No se puede marcar como READY la versión: la interpretación '{interp.Label}' tiene lower_bound ({interp.LowerBound}) mayor que upper_bound ({interp.UpperBound}).");
            }
        }

        // Continuidad y ausencia de solapamientos o discontinuidades (lower_bound == prev_upper + 1)
        for (int i = 1; i < sortedInterps.Count; i++)
        {
            var prevUpper = sortedInterps[i - 1].UpperBound;
            var currLower = sortedInterps[i].LowerBound;

            if (currLower != prevUpper + 1)
            {
                throw new DomainException(
                    "No se puede marcar como READY la versión: las interpretaciones presentan solapamientos o discontinuidades.");
            }
        }

        // Extremos del rango de interpretaciones vs puntuaciones posibles
        var interpMin = sortedInterps.First().LowerBound;
        var interpMax = sortedInterps.Last().UpperBound;

        if (interpMin != minScore || interpMax != maxScore)
        {
            throw new DomainException(
                $"No se puede marcar como READY la versión: el rango de interpretaciones [{interpMin}, {interpMax}] no coincide con el rango de puntuaciones posibles [{minScore}, {maxScore}].");
        }
    }
}
