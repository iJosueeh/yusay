using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Commands.DeleteCheckIn;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

/// <summary>
/// Reglas de eliminación de CheckIn (OQ-DOM-009 / RN-022 / RF-011): propiedad exclusiva
/// desde ICurrentUser, revisión esperada obligatoria, eliminación en cualquier momento
/// —sin ventana de 168 horas—, 404 uniforme para ajeno/inexistente/ya eliminado siempre
/// anterior al diagnóstico 409, y 409 solo para recurso propio con revisión desactualizada.
/// </summary>
public class DeleteCheckInUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private const string UniformNotFoundMessage = "El CheckIn solicitado no existe.";

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCheckInRepository _checkInRepo = new() { DatabaseTimestamp = Now };
    private readonly Guid _dimensionId = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();
    private readonly Guid _checkInId = Guid.NewGuid();

    public DeleteCheckInUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_ownerId);
    }

    private DeleteCheckInUseCase CreateUseCase() => new(
        _currentUser,
        _unitOfWork,
        _checkInRepo);

    private CheckIn ArrangeCheckIn(DateTimeOffset? createdAt = null, Guid? ownerId = null)
    {
        var checkIn = CheckIn.Rehydrate(
            _checkInId,
            ownerId ?? _ownerId,
            createdAt ?? Now,
            createdAt ?? Now,
            updatedAt: null,
            revision: 1,
            note: "Original",
            measurements: [Measurement.Create(_dimensionId, _versionId, 3)]);
        _checkInRepo.CheckIns.Add(checkIn);
        return checkIn;
    }

    [Fact]
    public async Task ExecuteAsync_WithMatchingRevision_ShouldDeleteOwnedCheckIn()
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        await useCase.ExecuteAsync(new DeleteCheckInCommand(_checkInId, 1));

        Assert.True(_unitOfWork.TransactionExecuted);
        Assert.Empty(_checkInRepo.CheckIns);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorized()
    {
        ArrangeCheckIn();
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => useCase.ExecuteAsync(new DeleteCheckInCommand(_checkInId, 1)));

        Assert.Single(_checkInRepo.CheckIns);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutRevision_ShouldThrowValidation()
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new DeleteCheckInCommand(_checkInId, null)));

        Assert.Contains("obligatoria", exception.Message, StringComparison.Ordinal);
        Assert.False(_unitOfWork.TransactionExecuted);
        Assert.Single(_checkInRepo.CheckIns);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_WithNonPositiveRevision_ShouldThrowValidation(int revision)
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new DeleteCheckInCommand(_checkInId, revision)));

        Assert.Contains("obligatoria", exception.Message, StringComparison.Ordinal);
        Assert.Single(_checkInRepo.CheckIns);
    }

    [Fact]
    public async Task ExecuteAsync_ForNonexistentCheckIn_ShouldThrowUniformNotFound()
    {
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => useCase.ExecuteAsync(new DeleteCheckInCommand(Guid.NewGuid(), 1)));

        Assert.Equal(UniformNotFoundMessage, exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ForForeignCheckIn_ShouldThrowIdenticalUniformNotFound()
    {
        ArrangeCheckIn(ownerId: Guid.NewGuid());
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => useCase.ExecuteAsync(new DeleteCheckInCommand(_checkInId, 1)));

        Assert.Equal(UniformNotFoundMessage, exception.Message);
        Assert.Single(_checkInRepo.CheckIns);
    }

    [Fact]
    public async Task ExecuteAsync_ForForeignCheckInWithStaleRevision_ShouldThrowNotFoundNotConflict()
    {
        // La propiedad se verifica antes de diagnosticar la revisión: un recurso ajeno
        // jamás produce 409 aunque la revisión enviada tampoco coincida.
        ArrangeCheckIn(ownerId: Guid.NewGuid());
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => useCase.ExecuteAsync(new DeleteCheckInCommand(_checkInId, 999)));

        Assert.Equal(UniformNotFoundMessage, exception.Message);
        Assert.Single(_checkInRepo.CheckIns);
    }

    [Fact]
    public async Task ExecuteAsync_WithStaleRevision_ShouldThrowConflictAndPreserveState()
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => useCase.ExecuteAsync(new DeleteCheckInCommand(_checkInId, 2)));

        Assert.Contains("revisión vigente", exception.Message, StringComparison.Ordinal);

        var persisted = Assert.Single(_checkInRepo.CheckIns);
        Assert.Equal(1, persisted.Revision);
        Assert.Equal("Original", persisted.Note);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCheckInIsOlderThan168Hours_ShouldDeleteWithoutTemporalWindow()
    {
        // OQ-DOM-009: la eliminación no admite ventana temporal; un registro creado
        // 500 horas antes se elimina igualmente con su revisión correcta.
        ArrangeCheckIn(createdAt: Now.AddHours(-500));
        var useCase = CreateUseCase();

        await useCase.ExecuteAsync(new DeleteCheckInCommand(_checkInId, 1));

        Assert.Empty(_checkInRepo.CheckIns);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPersistenceFails_ShouldPropagateAndPreserveState()
    {
        ArrangeCheckIn();
        _checkInRepo.DeleteFailure = new InvalidOperationException("fallo físico simulado");
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new DeleteCheckInCommand(_checkInId, 1)));

        Assert.Single(_checkInRepo.CheckIns);
    }
}
