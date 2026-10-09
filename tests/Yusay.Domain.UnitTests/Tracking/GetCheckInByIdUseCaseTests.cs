using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Commands.CreateCheckIn;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;
using GetCheckInById = Yusay.Application.Tracking.Queries.GetCheckInById;

namespace Yusay.Domain.UnitTests.Tracking;

public class GetCheckInByIdUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeCurrentUser _currentUser = new(Guid.NewGuid());
    private readonly FakeCheckInRepository _checkInRepo = new();
    private readonly Guid _dimensionId = Guid.NewGuid();
    private readonly Guid _dimensionVersionId = Guid.NewGuid();

    private GetCheckInById.GetCheckInByIdUseCase CreateUseCase() => new(_currentUser, _checkInRepo);

    private CheckIn ArrangeOwnedCheckIn(Guid? ownerId = null)
    {
        var checkIn = CheckIn.Create(
            ownerId ?? _currentUser.UserId!.Value,
            Now,
            Now,
            "Nota propia",
            [Measurement.Create(_dimensionId, _dimensionVersionId, 4)]);
        _checkInRepo.CheckIns.Add(checkIn);
        return checkIn;
    }

    [Fact]
    public async Task ExecuteAsync_WithOwnedCheckIn_ShouldReturnFullAggregate()
    {
        var checkIn = ArrangeOwnedCheckIn();
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new GetCheckInById.GetCheckInByIdQuery(checkIn.CheckInId));

        Assert.Equal(checkIn.CheckInId, result.CheckInId);
        Assert.Equal(Now, result.RecordedAt);
        Assert.Equal(Now, result.CreatedAt);
        Assert.Null(result.UpdatedAt);
        Assert.Equal(1, result.Revision);
        Assert.Equal("Nota propia", result.Note);
        var measurement = Assert.Single(result.Measurements);
        Assert.Equal(_dimensionId, measurement.DimensionId);
        Assert.Equal(_dimensionVersionId, measurement.DimensionVersionId);
        Assert.Equal(4, measurement.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorized()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => useCase.ExecuteAsync(new GetCheckInById.GetCheckInByIdQuery(Guid.NewGuid())));
    }

    [Fact]
    public async Task ExecuteAsync_WithNonexistentCheckIn_ShouldThrowNotFoundWithStableMessage()
    {
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => useCase.ExecuteAsync(new GetCheckInById.GetCheckInByIdQuery(Guid.NewGuid())));

        Assert.Equal("El CheckIn solicitado no existe.", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithForeignCheckIn_ShouldThrowNotFoundWithSameMessageAsNonexistent()
    {
        // Propietario ajeno: el repositorio (filtro en SQL) devuelve null, igual que un id inexistente
        ArrangeOwnedCheckIn(ownerId: Guid.NewGuid());
        var useCase = CreateUseCase();

        var foreignException = await Assert.ThrowsAsync<NotFoundException>(
            () => useCase.ExecuteAsync(new GetCheckInById.GetCheckInByIdQuery(_checkInRepo.CheckIns[0].CheckInId)));
        var nonexistentException = await Assert.ThrowsAsync<NotFoundException>(
            () => useCase.ExecuteAsync(new GetCheckInById.GetCheckInByIdQuery(Guid.NewGuid())));

        // 404 uniforme: mensajes idénticos, sin distinción observable
        Assert.Equal(nonexistentException.Message, foreignException.Message);
    }
}
