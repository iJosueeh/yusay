using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Queries.ListDimensions;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

// B4 de RF-021: GET /dimensions. El caso de uso exige identidad autenticada y usa la
// operación publicable del contrato (solo versiones ACTIVE); la visibilidad real de
// estados se comprueba en integración contra PostgreSQL.
public class ListDimensionsUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new(Guid.NewGuid());
    private readonly FakeDimensionCatalogRepository _repository = new();

    private ListDimensionsUseCase CreateUseCase() => new(_currentUser, _repository);

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorizedWithoutQuerying()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(() => useCase.ExecuteAsync());

        Assert.Equal(0, _repository.ListPublishedCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAlwaysUseThePublishedCatalogOperation()
    {
        _repository.Items.Add(DimensionCatalogBuilder.Item(DimensionVersionStatus.Active));

        var result = await CreateUseCase().ExecuteAsync();

        Assert.Equal(1, _repository.ListPublishedCalls);
        Assert.Equal(0, _repository.ListCompleteCalls);
        Assert.Single(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCatalog_ShouldReturnEmptyList()
    {
        var result = await CreateUseCase().ExecuteAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExposeOnlyActiveVersionsAndHideDraftOnlyDimensions()
    {
        _repository.Items.Add(DimensionCatalogBuilder.Item(
            DimensionVersionStatus.Retired,
            DimensionVersionStatus.Active,
            DimensionVersionStatus.Draft));
        _repository.Items.Add(DimensionCatalogBuilder.Item(DimensionVersionStatus.Draft));

        var result = await CreateUseCase().ExecuteAsync();

        var visible = Assert.Single(result);
        Assert.Equal(DimensionVersionStatus.Active, Assert.Single(visible.Versions).Status);
    }
}
