using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Queries.GetDimensionById;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

// B4 de RF-021: GET /dimensions/{dimensionId}. El caso de uso exige identidad
// autenticada, usa la operación histórica del contrato (ACTIVE y RETIRED, nunca DRAFT) y
// traduce la ausencia de Dimension visible (inexistente o con solo DRAFT) al mismo 404
// uniforme.
public class GetDimensionByIdUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new(Guid.NewGuid());
    private readonly FakeDimensionCatalogRepository _repository = new();

    private GetDimensionByIdUseCase CreateUseCase() => new(_currentUser, _repository);

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorizedWithoutQuerying()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => useCase.ExecuteAsync(new GetDimensionByIdQuery(Guid.NewGuid())));

        Assert.Empty(_repository.HistoryCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAlwaysUseTheHistoricalOperation()
    {
        var entry = DimensionCatalogBuilder.Item(DimensionVersionStatus.Active);
        _repository.Items.Add(entry);

        var result = await CreateUseCase().ExecuteAsync(
            new GetDimensionByIdQuery(entry.Dimension.DimensionId));

        Assert.Equal(entry.Dimension.DimensionId, Assert.Single(_repository.HistoryCalls));
        Assert.Equal(entry.Dimension.DimensionId, result.DimensionId);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonexistentDimension_ShouldThrowNotFound()
    {
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => useCase.ExecuteAsync(new GetDimensionByIdQuery(Guid.NewGuid())));

        Assert.Equal("La Dimensión solicitada no existe.", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithOnlyDraftDimension_ShouldThrowTheSameUniformNotFound()
    {
        var entry = DimensionCatalogBuilder.Item(DimensionVersionStatus.Draft);
        _repository.Items.Add(entry);

        var draftOnly = await Assert.ThrowsAsync<NotFoundException>(
            () => CreateUseCase().ExecuteAsync(new GetDimensionByIdQuery(entry.Dimension.DimensionId)));
        var nonexistent = await Assert.ThrowsAsync<NotFoundException>(
            () => CreateUseCase().ExecuteAsync(new GetDimensionByIdQuery(Guid.NewGuid())));

        Assert.Equal(nonexistent.Message, draftOnly.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithVisibleVersions_ShouldPreserveDefinitionScaleAndAnchors()
    {
        var entry = DimensionCatalogBuilder.Item(
            DimensionVersionStatus.Retired, DimensionVersionStatus.Active);
        _repository.Items.Add(entry);

        var result = await CreateUseCase().ExecuteAsync(
            new GetDimensionByIdQuery(entry.Dimension.DimensionId));

        Assert.Equal(entry.Dimension.DimensionId, result.DimensionId);
        Assert.Equal(entry.Dimension.Code.Value, result.Code);
        Assert.Equal(entry.Dimension.Name, result.Name);
        Assert.Equal(2, result.Versions.Count);
        Assert.Contains(result.Versions, v => v.Status == DimensionVersionStatus.Retired);
        Assert.Contains(result.Versions, v => v.Status == DimensionVersionStatus.Active);
        Assert.All(result.Versions, v =>
        {
            Assert.Equal("Definición de la versión.", v.Definition);
            Assert.Equal((1, 5, 1), (v.MinValue, v.MaxValue, v.Step));
            Assert.Equal(2, v.Anchors.Count);
        });
    }
}
