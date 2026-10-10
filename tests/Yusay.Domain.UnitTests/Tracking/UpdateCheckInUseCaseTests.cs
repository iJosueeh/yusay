using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Commands.UpdateCheckIn;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

/// <summary>
/// Reglas de edición de CheckIn (OQ-DOM-008 / RN-022 / RF-011 / MP-PHYS-007): propiedad
/// exclusiva desde ICurrentUser, 404 uniforme para ajeno e inexistente (siempre anterior
/// al diagnóstico 409), ventana absoluta de 168 horas con límite superior estricto,
/// revisión optimista obligatoria e incremento exacto de uno, conjunto de dimensiones
/// inmutable y validación de valores contra la DimensionVersion almacenada —incluso
/// RETIRED— con aritmética entera sin overflow.
/// </summary>
public class UpdateCheckInUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private const string UniformNotFoundMessage = "El CheckIn solicitado no existe.";

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCheckInRepository _checkInRepo = new() { DatabaseTimestamp = Now };
    private readonly FakeDimensionVersionRepository _dimensionVersionRepo = new();
    private readonly Guid _dimensionId = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();
    private readonly Guid _otherDimensionId = Guid.NewGuid();
    private readonly Guid _otherVersionId = Guid.NewGuid();
    private readonly Guid _checkInId = Guid.NewGuid();

    public UpdateCheckInUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_ownerId);
    }

    private UpdateCheckInUseCase CreateUseCase() => new(
        _currentUser,
        _unitOfWork,
        _checkInRepo,
        _dimensionVersionRepo);

    private void ArrangeStoredScale(Guid versionId, int minValue = 1, int maxValue = 5, int step = 1) =>
        _dimensionVersionRepo.StoredScales[versionId] =
            new StoredDimensionScale(versionId, minValue, maxValue, step);

    private CheckIn ArrangeCheckIn(
        DateTimeOffset? createdAt = null,
        string? note = "Original",
        Guid? ownerId = null,
        bool registerScale = true)
    {
        if (registerScale)
        {
            ArrangeStoredScale(_versionId);
        }

        var instant = createdAt ?? Now;
        var checkIn = CheckIn.Rehydrate(
            _checkInId,
            ownerId ?? _ownerId,
            instant,
            instant,
            updatedAt: null,
            revision: 1,
            note: note,
            measurements: [Measurement.Create(_dimensionId, _versionId, 3)]);
        _checkInRepo.CheckIns.Add(checkIn);
        return checkIn;
    }

    private CheckIn ArrangeCheckInWithTwoDimensions()
    {
        ArrangeStoredScale(_versionId);
        ArrangeStoredScale(_otherVersionId, 0, 10, 1);
        var checkIn = CheckIn.Rehydrate(
            _checkInId,
            _ownerId,
            Now,
            Now,
            updatedAt: null,
            revision: 1,
            note: "Original",
            measurements:
            [
                Measurement.Create(_dimensionId, _versionId, 3),
                Measurement.Create(_otherDimensionId, _otherVersionId, 5)
            ]);
        _checkInRepo.CheckIns.Add(checkIn);
        return checkIn;
    }

    private UpdateCheckInCommand ValidCommand(
        int? revision = 1,
        DateTimeOffset? recordedAt = null,
        string? note = "Corregido",
        int value = 5) =>
        new(_checkInId, revision, recordedAt ?? Now, note, [new UpdateMeasurementInput(_dimensionId, value)]);

    private UpdateCheckInCommand CommandWith(
        int? revision = 1,
        DateTimeOffset? recordedAt = null,
        string? note = "Corregido",
        params UpdateMeasurementInput[] measurements) =>
        new(_checkInId, revision, recordedAt ?? Now, note, measurements);

    // ------------------------------------------------------------------------------------------
    // Edición válida: transacción única, revision + 1, updated_at e inmutabilidades
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithValidData_ShouldPersistUpdateAndIncrementRevisionExactlyOnce()
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(ValidCommand(note: "Corregido", value: 5));

        Assert.True(_unitOfWork.TransactionExecuted);
        Assert.Equal(_checkInId, result.CheckInId);
        Assert.Equal(2, result.Revision);
        Assert.Equal(Now, result.UpdatedAt);
        Assert.Equal("Corregido", result.Note);
        Assert.Equal(Now, result.RecordedAt);

        var persisted = Assert.Single(_checkInRepo.CheckIns);
        Assert.Equal(2, persisted.Revision);
        Assert.Equal(Now, persisted.UpdatedAt);
        Assert.Equal(_ownerId, persisted.UserId);       // user_id inmutable
        Assert.Equal(Now, persisted.CreatedAt);         // created_at inmutable
        Assert.Equal("Corregido", persisted.Note);

        var measurement = Assert.Single(persisted.Measurements);
        Assert.Equal(_dimensionId, measurement.DimensionId);            // conjunto inmutable
        Assert.Equal(_versionId, measurement.DimensionVersionId);       // versión almacenada preservada
        Assert.Equal(5, measurement.Value);

        var resultMeasurement = Assert.Single(result.Measurements);
        Assert.Equal(_versionId, resultMeasurement.DimensionVersionId);
        Assert.Equal(5, resultMeasurement.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorized()
    {
        ArrangeCheckIn();
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(() => useCase.ExecuteAsync(ValidCommand()));

        Assert.Equal(1, Assert.Single(_checkInRepo.CheckIns).Revision);
    }

    // ------------------------------------------------------------------------------------------
    // Validaciones de contrato: revision obligatoria, recordedAt y measurements obligatorios
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithoutRevision_ShouldThrowValidation()
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(ValidCommand(revision: null)));

        Assert.Contains("obligatoria", exception.Message, StringComparison.Ordinal);
        Assert.False(_unitOfWork.TransactionExecuted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_WithNonPositiveRevision_ShouldThrowValidation(int revision)
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(ValidCommand(revision: revision)));

        Assert.Contains("obligatoria", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, Assert.Single(_checkInRepo.CheckIns).Revision);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutRecordedAt_ShouldThrowValidation()
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();
        var command = new UpdateCheckInCommand(
            _checkInId, 1, null, "Corregido", [new UpdateMeasurementInput(_dimensionId, 5)]);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(command));

        Assert.Contains("recordedAt", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutMeasurements_ShouldThrowValidation()
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();
        var command = new UpdateCheckInCommand(_checkInId, 1, Now, null, null);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyMeasurements_ShouldThrowValidation()
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();
        var command = new UpdateCheckInCommand(
            _checkInId, 1, Now, null, Array.Empty<UpdateMeasurementInput>());

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    // ------------------------------------------------------------------------------------------
    // Propiedad exclusiva: 404 uniforme siempre anterior al diagnóstico 409
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ForNonexistentCheckIn_ShouldThrowUniformNotFound()
    {
        var useCase = CreateUseCase();
        var command = new UpdateCheckInCommand(
            Guid.NewGuid(), 1, Now, "x", [new UpdateMeasurementInput(_dimensionId, 5)]);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal(UniformNotFoundMessage, exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ForForeignCheckIn_ShouldThrowIdenticalUniformNotFound()
    {
        var foreignOwnerId = Guid.NewGuid();
        ArrangeCheckIn(ownerId: foreignOwnerId);
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => useCase.ExecuteAsync(ValidCommand()));

        Assert.Equal(UniformNotFoundMessage, exception.Message);
        Assert.Equal(1, Assert.Single(_checkInRepo.CheckIns).Revision);
    }

    [Fact]
    public async Task ExecuteAsync_ForForeignCheckInWithStaleRevision_ShouldThrowNotFoundNotConflict()
    {
        // La propiedad se verifica antes de diagnosticar la revisión: un recurso ajeno
        // nunca produce 409 aunque la revisión enviada tampoco coincida.
        ArrangeCheckIn(ownerId: Guid.NewGuid());
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => useCase.ExecuteAsync(ValidCommand(revision: 999)));

        Assert.Equal(UniformNotFoundMessage, exception.Message);
        Assert.Equal(1, Assert.Single(_checkInRepo.CheckIns).Revision);
    }

    [Fact]
    public async Task ExecuteAsync_WithStaleRevision_ShouldThrowConflictAndPreserveState()
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => useCase.ExecuteAsync(ValidCommand(revision: 2)));

        Assert.Contains("revisión vigente", exception.Message, StringComparison.Ordinal);

        var persisted = Assert.Single(_checkInRepo.CheckIns);
        Assert.Equal(1, persisted.Revision);
        Assert.Null(persisted.UpdatedAt);
        Assert.Equal("Original", persisted.Note);
        Assert.Equal(3, Assert.Single(persisted.Measurements).Value);
    }

    // ------------------------------------------------------------------------------------------
    // Ventana absoluta de 168 horas con límite superior estricto
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenEditWindowReachesStrictLimit_ShouldThrowConflict()
    {
        // Límite superior estricto: en el instante exacto created_at + 168 h la ventana ya está cerrada.
        var createdAt = Now.AddHours(-168);
        ArrangeCheckIn(createdAt: createdAt);
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => useCase.ExecuteAsync(ValidCommand(recordedAt: createdAt)));

        Assert.Contains("ventana", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, Assert.Single(_checkInRepo.CheckIns).Revision);
    }

    [Fact]
    public async Task ExecuteAsync_WhileEditWindowRemainsOpen_ShouldSucceed()
    {
        var createdAt = Now.AddHours(-167);
        ArrangeCheckIn(createdAt: createdAt);
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(ValidCommand(recordedAt: createdAt));

        Assert.Equal(2, result.Revision);
    }

    [Theory]
    [InlineData(1)]     // recordedAt posterior a created_at
    [InlineData(-169)]  // recordedAt anterior a created_at - 168 h
    public async Task ExecuteAsync_WithRecordedAtOutsideStoredWindow_ShouldThrowValidation(int hoursOffset)
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(ValidCommand(recordedAt: Now.AddHours(hoursOffset))));

        Assert.Contains("168 horas", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, Assert.Single(_checkInRepo.CheckIns).Revision);
    }

    [Theory]
    [InlineData(0)]      // recordedAt == created_at (límite superior inclusivo)
    [InlineData(-168)]   // recordedAt == created_at - 168 h (límite inferior inclusivo)
    public async Task ExecuteAsync_WithRecordedAtAtExactWindowEdges_ShouldSucceed(int hoursOffset)
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(ValidCommand(recordedAt: Now.AddHours(hoursOffset)));

        Assert.Equal(2, result.Revision);
        Assert.Equal(Now.AddHours(hoursOffset), result.RecordedAt);
    }

    // ------------------------------------------------------------------------------------------
    // Conjunto de dimensiones inmutable y validación contra la versión almacenada
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithUnknownDimensionInBody_ShouldThrowValidation_ImmutableSet()
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(CommandWith(measurements: new UpdateMeasurementInput(Guid.NewGuid(), 3))));

        Assert.Contains("inmutable", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, Assert.Single(_checkInRepo.CheckIns).Revision);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingStoredDimensionInBody_ShouldThrowValidation_ImmutableSet()
    {
        ArrangeCheckInWithTwoDimensions();
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(CommandWith(measurements: new UpdateMeasurementInput(_dimensionId, 4))));

        Assert.Contains("inmutable", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, Assert.Single(_checkInRepo.CheckIns).Measurements.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WithRepeatedDimensionInBody_ShouldThrowValidation_RN030()
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(CommandWith(
                1,
                null,
                "Corregido",
                new UpdateMeasurementInput(_dimensionId, 4),
                new UpdateMeasurementInput(_dimensionId, 5))));

        Assert.Contains("más de una Measurement de la Dimensión", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]   // bajo el mínimo
    [InlineData(6)]   // sobre el máximo
    public async Task ExecuteAsync_WithValueOutsideStoredScale_ShouldThrowValidation(int value)
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(ValidCommand(value: value)));

        Assert.Contains("fuera de la escala", exception.Message, StringComparison.Ordinal);
        Assert.Equal(3, Assert.Single(Assert.Single(_checkInRepo.CheckIns).Measurements).Value);
    }

    [Fact]
    public async Task ExecuteAsync_WithMisalignedStep_ShouldThrowValidation()
    {
        ArrangeCheckIn();
        ArrangeStoredScale(_versionId, 1, 9, 2); // valores válidos: 1, 3, 5, 7, 9
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(ValidCommand(value: 4)));

        Assert.Contains("no es alcanzable con paso", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithExtremeScaleValues_ShouldNotOverflow()
    {
        // (value − min) desbordaría en int: la validación usa aritmética de 64 bits (OQ-DOM-008).
        ArrangeCheckIn();
        ArrangeStoredScale(_versionId, int.MinValue, int.MaxValue, 1);
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(ValidCommand(value: int.MaxValue));

        Assert.Equal(2, result.Revision);
        Assert.Equal(int.MaxValue, Assert.Single(result.Measurements).Value);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutActiveVersion_ShouldValidateAgainstStoredScale_EvenRetired()
    {
        // Ninguna versión ACTIVE: la validación se resuelve por la versión almacenada (RETIRED).
        ArrangeCheckIn();
        Assert.Empty(_dimensionVersionRepo.ActiveScales);
        var useCase = CreateUseCase();

        var invalid = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(ValidCommand(value: 7)));
        Assert.Contains("fuera de la escala", invalid.Message, StringComparison.Ordinal);

        var result = await useCase.ExecuteAsync(ValidCommand(value: 4));

        Assert.Equal(2, result.Revision);
        Assert.Equal(_versionId, Assert.Single(result.Measurements).DimensionVersionId);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnregisteredStoredVersion_ShouldThrowConflict()
    {
        // La FK garantiza la existencia en PostgreSQL; en dominio se degrada a 409 sin escribir.
        ArrangeCheckIn(registerScale: false);
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => useCase.ExecuteAsync(ValidCommand()));

        Assert.Contains("DimensionVersion registrada", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, Assert.Single(_checkInRepo.CheckIns).Revision);
    }

    // ------------------------------------------------------------------------------------------
    // Note y fallos de persistencia
    // ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ExecuteAsync_WithEmptyNote_ShouldClearNote(string? note)
    {
        ArrangeCheckIn();
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(ValidCommand(note: note));

        Assert.Null(result.Note);
        Assert.Null(Assert.Single(_checkInRepo.CheckIns).Note);
    }

    [Fact]
    public async Task ExecuteAsync_WithVeryLongNote_ShouldPersistWithoutLengthLimit()
    {
        // RN-041: Note es texto libre sin límite artificial (D-F2a-05 revisada).
        ArrangeCheckIn();
        var useCase = CreateUseCase();
        var longNote = new string('a', 5000);

        var result = await useCase.ExecuteAsync(ValidCommand(note: longNote));

        Assert.Equal(longNote, result.Note);
        Assert.Equal(longNote, Assert.Single(_checkInRepo.CheckIns).Note);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPersistenceFails_ShouldPropagateAndPreserveState()
    {
        ArrangeCheckIn();
        _checkInRepo.UpdateFailure = new InvalidOperationException("fallo físico simulado");
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(ValidCommand()));

        var persisted = Assert.Single(_checkInRepo.CheckIns);
        Assert.Equal(1, persisted.Revision);
        Assert.Null(persisted.UpdatedAt);
        Assert.Equal("Original", persisted.Note);
    }
}
