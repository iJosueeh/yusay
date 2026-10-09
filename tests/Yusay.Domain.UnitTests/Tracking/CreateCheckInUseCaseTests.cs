using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Commands.CreateCheckIn;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

public class CreateCheckInUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeCurrentUser _currentUser = new(Guid.NewGuid());
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCheckInRepository _checkInRepo = new() { DatabaseTimestamp = Now };
    private readonly FakeDimensionVersionRepository _dimensionVersionRepo = new();
    private readonly Guid _dimensionId = Guid.NewGuid();

    private CreateCheckInUseCase CreateUseCase() => new(
        _currentUser,
        _unitOfWork,
        _checkInRepo,
        _dimensionVersionRepo);

    private void ArrangeActiveDimension(int minValue = 1, int maxValue = 5, int step = 1)
    {
        _dimensionVersionRepo.ActiveScales[_dimensionId] =
            new ActiveDimensionScale(Guid.NewGuid(), minValue, maxValue, step);
    }

    private static CreateCheckInCommand CommandWith(
        Guid dimensionId,
        int value,
        DateTimeOffset? recordedAt = null,
        string? note = null) =>
        new(recordedAt, note, [new CreateMeasurementInput(dimensionId, value)]);

    [Fact]
    public async Task ExecuteAsync_WithValidData_ShouldCreateOwnedCheckInWithResolvedVersion()
    {
        ArrangeActiveDimension();
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(CommandWith(_dimensionId, 3));

        Assert.True(_unitOfWork.TransactionExecuted);
        Assert.Equal(1, result.Revision);
        Assert.Equal(Now, result.RecordedAt); // recordedAt omitido ⇒ igualado al instante capturado
        Assert.Equal(Now, result.CreatedAt);

        var persisted = Assert.Single(_checkInRepo.CheckIns);
        Assert.Equal(_currentUser.UserId, persisted.UserId);
        Assert.Equal(result.CheckInId, persisted.CheckInId);
        var measurement = Assert.Single(persisted.Measurements);
        Assert.Equal(_dimensionId, measurement.DimensionId);
        Assert.Equal(_dimensionVersionRepo.ActiveScales[_dimensionId].DimensionVersionId, measurement.DimensionVersionId);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorized()
    {
        _currentUser.UserId = null;
        ArrangeActiveDimension();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => useCase.ExecuteAsync(CommandWith(_dimensionId, 3)));

        Assert.Empty(_checkInRepo.CheckIns);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyMeasurements_ShouldThrowValidation()
    {
        var useCase = CreateUseCase();
        var command = new CreateCheckInCommand(Now, null, Array.Empty<CreateMeasurementInput>());

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullMeasurements_ShouldThrowValidation()
    {
        var useCase = CreateUseCase();
        var command = new CreateCheckInCommand(Now, null, null!);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task ExecuteAsync_WithRepeatedDimension_ShouldThrowValidation_RN030()
    {
        ArrangeActiveDimension();
        var useCase = CreateUseCase();
        var command = new CreateCheckInCommand(Now, null,
        [
            new CreateMeasurementInput(_dimensionId, 2),
            new CreateMeasurementInput(_dimensionId, 4)
        ]);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleDimensions_ShouldPersistAllMeasurements()
    {
        ArrangeActiveDimension(1, 5, 1);
        var otherDimension = Guid.NewGuid();
        _dimensionVersionRepo.ActiveScales[otherDimension] = new ActiveDimensionScale(Guid.NewGuid(), 0, 10, 2);
        var useCase = CreateUseCase();
        var command = new CreateCheckInCommand(Now, "Día regular", 
        [
            new CreateMeasurementInput(_dimensionId, 4),
            new CreateMeasurementInput(otherDimension, 6)
        ]);

        var result = await useCase.ExecuteAsync(command);

        var persisted = Assert.Single(_checkInRepo.CheckIns);
        Assert.Equal(2, persisted.Measurements.Count);
        Assert.Equal("Día regular", persisted.Note);
        Assert.Equal(result.CheckInId, persisted.CheckInId);
    }

    [Theory]
    [InlineData(0)]   // bajo min
    [InlineData(6)]   // sobre max
    public async Task ExecuteAsync_WithValueOutsideScale_ShouldThrowValidation(int value)
    {
        ArrangeActiveDimension(1, 5, 1);
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(CommandWith(_dimensionId, value)));

        Assert.Empty(_checkInRepo.CheckIns);
    }

    [Fact]
    public async Task ExecuteAsync_WithValueMisalignedWithStep_ShouldThrowValidation()
    {
        ArrangeActiveDimension(1, 10, 2); // valores válidos: 1, 3, 5, 7, 9
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(CommandWith(_dimensionId, 4)));
    }

    [Fact]
    public async Task ExecuteAsync_WithScaleExtremes_ShouldNotOverflow()
    {
        // min/max en extremos del rango entero: la resta (value − min) desbordaría en int
        ArrangeActiveDimension(int.MinValue, int.MaxValue, 1);
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(CommandWith(_dimensionId, int.MaxValue));

        Assert.Equal(1, result.Revision);
        Assert.Single(_checkInRepo.CheckIns);
    }

    [Fact]
    public async Task ExecuteAsync_WithRecordedAtInFuture_ShouldThrowValidation()
    {
        ArrangeActiveDimension();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(CommandWith(_dimensionId, 3, recordedAt: Now.AddMinutes(1))));
    }

    [Fact]
    public async Task ExecuteAsync_WithRecordedAtOlderThan168Hours_ShouldThrowValidation()
    {
        ArrangeActiveDimension();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(CommandWith(_dimensionId, 3, recordedAt: Now.AddHours(-169))));
    }

    [Fact]
    public async Task ExecuteAsync_WithRecordedAtExactlyAtWindowEdge_ShouldSucceed()
    {
        ArrangeActiveDimension();
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(
            CommandWith(_dimensionId, 3, recordedAt: Now.AddHours(-168)));

        Assert.Equal(Now.AddHours(-168), result.RecordedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownDimension_ShouldThrowNotFound()
    {
        var unknownDimension = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<NotFoundException>(
            () => useCase.ExecuteAsync(CommandWith(unknownDimension, 3)));

        Assert.Empty(_checkInRepo.CheckIns);
    }

    [Fact]
    public async Task ExecuteAsync_WithDimensionWithoutActiveVersion_ShouldThrowConflict()
    {
        _dimensionVersionRepo.ExistingDimensions.Add(_dimensionId); // existe pero sin versión activa
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ConflictException>(
            () => useCase.ExecuteAsync(CommandWith(_dimensionId, 3)));

        Assert.Empty(_checkInRepo.CheckIns);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ExecuteAsync_WithEmptyNote_ShouldPersistNull(string? note)
    {
        ArrangeActiveDimension();
        var useCase = CreateUseCase();

        await useCase.ExecuteAsync(CommandWith(_dimensionId, 3, note: note));

        Assert.Null(Assert.Single(_checkInRepo.CheckIns).Note);
    }

    [Fact]
    public async Task ExecuteAsync_WithVeryLongNote_ShouldPersistWithoutLengthLimit()
    {
        // Sin límite artificial de longitud: RN-041 define Note como texto libre (D-F2a-05 revisada)
        ArrangeActiveDimension();
        var useCase = CreateUseCase();
        var longNote = new string('a', 5000);

        await useCase.ExecuteAsync(CommandWith(_dimensionId, 3, note: longNote));

        Assert.Equal(longNote, Assert.Single(_checkInRepo.CheckIns).Note);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPersistenceFails_ShouldPropagateAndPersistNothing()
    {
        ArrangeActiveDimension();
        _checkInRepo.CreateFailure = new InvalidOperationException("fallo físico simulado");
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(CommandWith(_dimensionId, 3)));

        Assert.Empty(_checkInRepo.CheckIns);
    }
}
