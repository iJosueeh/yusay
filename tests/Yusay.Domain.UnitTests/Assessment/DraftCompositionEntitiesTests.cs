using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Common;

namespace Yusay.Domain.UnitTests.Assessment;

public class DraftCompositionEntitiesTests
{
    [Fact]
    public void Question_Create_WithValidData_ShouldInitialize()
    {
        var qId = Guid.NewGuid();
        var vId = Guid.NewGuid();
        var q = Question.Create(vId, 1, "¿Se ha sentido desanimado?", true, qId);

        Assert.Equal(qId, q.QuestionId);
        Assert.Equal(vId, q.InstrumentVersionId);
        Assert.Equal(1, q.Position);
        Assert.Equal("¿Se ha sentido desanimado?", q.Prompt);
        Assert.True(q.Required);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Question_Create_WithEmptyPrompt_ShouldThrowDomainException(string prompt)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Question.Create(Guid.NewGuid(), 1, prompt, true));
        Assert.Contains("enunciado", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Question_Create_WithInvalidPosition_ShouldThrowDomainException(int position)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Question.Create(Guid.NewGuid(), position, "Prompt", true));
        Assert.Contains("posición", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnswerOption_Create_WithValidData_ShouldInitialize()
    {
        var optId = Guid.NewGuid();
        var qId = Guid.NewGuid();
        var opt = AnswerOption.Create(qId, 1, "Varios días", optId);

        Assert.Equal(optId, opt.OptionId);
        Assert.Equal(qId, opt.QuestionId);
        Assert.Equal(1, opt.Position);
        Assert.Equal("Varios días", opt.Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnswerOption_Create_WithEmptyLabel_ShouldThrowDomainException(string label)
    {
        var ex = Assert.Throws<DomainException>(() =>
            AnswerOption.Create(Guid.NewGuid(), 1, label));
        Assert.Contains("etiqueta", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnswerOption_Create_WithInvalidPosition_ShouldThrowDomainException(int position)
    {
        var ex = Assert.Throws<DomainException>(() =>
            AnswerOption.Create(Guid.NewGuid(), position, "Label"));
        Assert.Contains("posición", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScoringContribution_Create_WithValidData_ShouldInitialize()
    {
        var optId = Guid.NewGuid();
        var qId = Guid.NewGuid();
        var vId = Guid.NewGuid();
        var sc = new ScoringContribution(optId, qId, vId, 3);

        Assert.Equal(optId, sc.OptionId);
        Assert.Equal(qId, sc.QuestionId);
        Assert.Equal(vId, sc.InstrumentVersionId);
        Assert.Equal(3, sc.Contribution);
    }

    [Fact]
    public void Interpretation_Create_WithValidData_ShouldInitialize()
    {
        var interpId = Guid.NewGuid();
        var vId = Guid.NewGuid();
        var interp = Interpretation.Create(
            vId, "Depresión moderada", "Puntaje de 10 a 14", "No es diagnóstico clínico", 10, 14, interpId);

        Assert.Equal(interpId, interp.InterpretationId);
        Assert.Equal(vId, interp.InstrumentVersionId);
        Assert.Equal("Depresión moderada", interp.Label);
        Assert.Equal("Puntaje de 10 a 14", interp.Description);
        Assert.Equal("No es diagnóstico clínico", interp.Limitations);
        Assert.Equal(10, interp.LowerBound);
        Assert.Equal(14, interp.UpperBound);
    }

    [Fact]
    public void Interpretation_Create_WithLowerGreaterThanUpper_ShouldThrowDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Interpretation.Create(Guid.NewGuid(), "Rango", "Desc", null, 15, 10));
        Assert.Contains("límite inferior", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Interpretation_Create_WithEmptyLabel_ShouldThrowDomainException(string label)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Interpretation.Create(Guid.NewGuid(), label, "Desc", null, 0, 10));
        Assert.Contains("etiqueta", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstrumentVersionReference_Create_WithValidData_ShouldInitialize()
    {
        var vId = Guid.NewGuid();
        var r = new InstrumentVersionReference(vId, 1, "Kroenke et al. (2001)", "https://doi.org/10.1046/j.1525-1497.2001.016009606.x");

        Assert.Equal(vId, r.InstrumentVersionId);
        Assert.Equal(1, r.ReferenceOrder);
        Assert.Equal("Kroenke et al. (2001)", r.Citation);
        Assert.Equal("https://doi.org/10.1046/j.1525-1497.2001.016009606.x", r.Url);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InstrumentVersionReference_Create_WithNonPositiveOrder_ShouldThrowDomainException(int order)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new InstrumentVersionReference(Guid.NewGuid(), order, "Citation", null));
        Assert.Contains("orden", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ftp://invalid-scheme.com")]
    [InlineData("not-a-valid-url")]
    public void InstrumentVersionReference_Create_WithInvalidUrl_ShouldThrowDomainException(string url)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new InstrumentVersionReference(Guid.NewGuid(), 1, "Citation", url));
        Assert.Contains("URL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuditEvent_CreateInstrumentVersionUpdated_WithApprovedCategories_ShouldCreateValidProfileC()
    {
        var adminId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var categories = new[] { "QUESTIONS", "SCORING" };

        var audit = AuditEvent.CreateInstrumentVersionUpdated(versionId, adminId, categories);

        Assert.Equal("CATALOG_UPDATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(adminId, audit.ActorUserId);
        Assert.Equal("INSTRUMENT_VERSION", audit.TargetType);
        Assert.Equal(versionId.ToString(), audit.TargetIdentifier);
        Assert.NotNull(audit.Metadata);
        Assert.Contains("\"changed_fields\":[\"QUESTIONS\",\"SCORING\"]", audit.Metadata);
    }

    [Fact]
    public void AuditEvent_CreateInstrumentVersionUpdated_WithUnapprovedCategory_ShouldThrowDomainException()
    {
        var adminId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var categories = new[] { "NON_EXISTENT_CATEGORY" };

        var ex = Assert.Throws<DomainException>(() =>
            AuditEvent.CreateInstrumentVersionUpdated(adminId, versionId, categories));

        Assert.Contains("categoría", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
