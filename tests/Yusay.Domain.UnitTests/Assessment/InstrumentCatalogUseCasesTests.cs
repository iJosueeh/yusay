using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Queries.GetPublishedInstrumentById;
using Yusay.Application.Assessment.Queries.ListPublishedInstruments;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public class InstrumentCatalogUseCasesTests
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeInstrumentCatalogRepository _repository = new();

    private readonly ListPublishedInstrumentsUseCase _listUseCase;
    private readonly GetPublishedInstrumentByIdUseCase _getByIdUseCase;

    public InstrumentCatalogUseCasesTests()
    {
        _currentUser.UserId = Guid.NewGuid();
        _listUseCase = new ListPublishedInstrumentsUseCase(_currentUser, _repository);
        _getByIdUseCase = new GetPublishedInstrumentByIdUseCase(_currentUser, _repository);
    }

    [Fact]
    public async Task ListPublishedInstruments_WithoutAuthentication_ShouldThrowUnauthorized()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _listUseCase.ExecuteAsync());
    }

    [Fact]
    public async Task ListPublishedInstruments_WithAuthenticatedUser_ShouldReturnCatalogItems()
    {
        var item = new PublishedInstrumentSummaryDto(
            Guid.NewGuid(),
            "PHQ-9",
            "Cuestionario de Salud",
            "Depresión",
            "Despistaje",
            new PublishedVersionSummaryDto(
                Guid.NewGuid(),
                1,
                "Fuente",
                "Adultos",
                null,
                null,
                "Limitaciones"));
        _repository.Summaries.Add(item);

        var result = await _listUseCase.ExecuteAsync();

        Assert.Single(result);
        Assert.Equal("PHQ-9", result[0].Code);
    }

    [Fact]
    public async Task GetPublishedInstrumentById_WithoutAuthentication_ShouldThrowUnauthorized()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _getByIdUseCase.ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPublishedInstrumentById_WhenNotFoundOrNotPublished_ShouldThrowNotFound()
    {
        var nonExistent = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _getByIdUseCase.ExecuteAsync(nonExistent));

        Assert.Contains("no existe o no tiene una versión publicada", ex.Message);
    }

    [Fact]
    public async Task GetPublishedInstrumentById_WhenPublished_ShouldReturnDetailsWithReferences()
    {
        var instrumentId = Guid.NewGuid();
        var references = new List<PublishedInstrumentReferenceDto>
        {
            new(1, "Citation 1", "https://example.com/1")
        };
        var details = new PublishedInstrumentDetailsDto(
            instrumentId,
            "PHQ-9",
            "Cuestionario de Salud",
            "Depresión",
            "Despistaje",
            new PublishedVersionDetailsDto(
                Guid.NewGuid(),
                1,
                "Fuente",
                "Adultos",
                "Ambiente tranquilo",
                "Dominio público",
                "Limitaciones psicométricas",
                references));
        _repository.Details[instrumentId] = details;

        var result = await _getByIdUseCase.ExecuteAsync(instrumentId);

        Assert.Equal(instrumentId, result.InstrumentId);
        Assert.Equal("PHQ-9", result.Code);
        Assert.Single(result.Version.References);
        Assert.Equal("Citation 1", result.Version.References[0].Citation);
    }
}
