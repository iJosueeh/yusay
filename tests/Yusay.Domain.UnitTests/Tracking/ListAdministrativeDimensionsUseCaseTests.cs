using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Queries.ListAdministrativeDimensions;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

// B4 de RF-021: GET /admin/dimensions. El caso de uso exige identidad autenticada y usa
// la operación de catálogo completo del contrato (DRAFT incluido y dimensiones sin
// versión); la denegación a usuarios sin habilitación administrativa la resuelve la
// política Administrator en el controlador, no el caso de uso.
public class ListAdministrativeDimensionsUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new(Guid.NewGuid());
    private readonly FakeDimensionCatalogRepository _repository = new();

    private ListAdministrativeDimensionsUseCase CreateUseCase() => new(_currentUser, _repository);

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorizedWithoutQuerying()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(() => useCase.ExecuteAsync());

        Assert.Equal(0, _repository.ListCompleteCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAlwaysUseTheCompleteCatalogOperation()
    {
        _repository.Items.Add(DimensionCatalogBuilder.Item(DimensionVersionStatus.Draft));

        var result = await CreateUseCase().ExecuteAsync();

        Assert.Equal(1, _repository.ListCompleteCalls);
        Assert.Equal(0, _repository.ListPublishedCalls);
        Assert.Single(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCatalog_ShouldReturnEmptyList()
    {
        var result = await CreateUseCase().ExecuteAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExposeDraftsAndDimensionsWithoutVersions()
    {
        _repository.Items.Add(DimensionCatalogBuilder.Item(
            DimensionVersionStatus.Draft,
            DimensionVersionStatus.Active,
            DimensionVersionStatus.Retired));
        var withoutVersions = DimensionCatalogBuilder.Item();
        _repository.Items.Add(withoutVersions);

        var result = await CreateUseCase().ExecuteAsync();

        Assert.Equal(2, result.Count);
        var draft = result.Single(item =>
            item.Versions.Any(version => version.Status == DimensionVersionStatus.Draft));
        Assert.Equal(3, draft.Versions.Count);
        var pending = Assert.Single(result,
            item => item.DimensionId == withoutVersions.Dimension.DimensionId);
        Assert.Empty(pending.Versions);
    }
}
