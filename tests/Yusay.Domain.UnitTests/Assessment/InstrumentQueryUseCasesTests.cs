using Yusay.Application.Assessment.Commands.CreateInstrument;
using Yusay.Application.Assessment.Commands.CreateInstrumentVersion;
using Yusay.Application.Assessment.Queries.GetAdministrativeInstrumentById;
using Yusay.Application.Assessment.Queries.GetAdministrativeInstrumentVersionById;
using Yusay.Application.Assessment.Queries.ListAdministrativeInstruments;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public class InstrumentQueryUseCasesTests
{
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeInstrumentAdministrationRepository _repository = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();

    public InstrumentQueryUseCasesTests()
    {
        _currentUser = new FakeCurrentUser(_adminUserId);
    }

    [Fact]
    public async Task ListAdministrativeInstruments_ShouldReturnSummaries()
    {
        var createInst = new CreateInstrumentUseCase(_currentUser, _unitOfWork, _repository, _auditEventRepo);
        var createVer = new CreateInstrumentVersionUseCase(_currentUser, _unitOfWork, _repository, _auditEventRepo);

        var inst1 = await createInst.ExecuteAsync(new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc", "Propósito"));
        await createVer.ExecuteAsync(new CreateInstrumentVersionCommand(inst1.InstrumentId, 1, "F1", "P1", null, null, "L1"));
        await createVer.ExecuteAsync(new CreateInstrumentVersionCommand(inst1.InstrumentId, 2, "F2", "P2", null, null, "L2"));

        var listQuery = new ListAdministrativeInstrumentsUseCase(_currentUser, _repository);
        var list = await listQuery.ExecuteAsync();

        var item = Assert.Single(list);
        Assert.Equal(inst1.InstrumentId, item.InstrumentId);
        Assert.Equal(2, item.TotalVersions);
    }

    [Fact]
    public async Task GetAdministrativeInstrumentById_WithExistingId_ShouldReturnDetailsWithVersions()
    {
        var createInst = new CreateInstrumentUseCase(_currentUser, _unitOfWork, _repository, _auditEventRepo);
        var createVer = new CreateInstrumentVersionUseCase(_currentUser, _unitOfWork, _repository, _auditEventRepo);

        var inst = await createInst.ExecuteAsync(new CreateInstrumentCommand("gad-7", "GAD-7 Ansiedad", "Desc", "Propósito"));
        await createVer.ExecuteAsync(new CreateInstrumentVersionCommand(inst.InstrumentId, 1, "GAD-7 v1.0", "Población", null, null, "Limitaciones"));

        var getQuery = new GetAdministrativeInstrumentByIdUseCase(_currentUser, _repository);
        var details = await getQuery.ExecuteAsync(inst.InstrumentId);

        Assert.Equal(inst.InstrumentId, details.InstrumentId);
        Assert.Equal("gad-7", details.Code);
        Assert.Single(details.Versions);
        Assert.Equal(1, details.Versions[0].Version);
    }

    [Fact]
    public async Task GetAdministrativeInstrumentById_WithNonExistentId_ShouldThrowNotFoundException()
    {
        var getQuery = new GetAdministrativeInstrumentByIdUseCase(_currentUser, _repository);
        var missingId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            getQuery.ExecuteAsync(missingId));

        Assert.Contains("no existe", ex.Message);
    }

    [Fact]
    public async Task GetAdministrativeInstrumentVersionById_WithExistingId_ShouldReturnVersionDetails()
    {
        var createInst = new CreateInstrumentUseCase(_currentUser, _unitOfWork, _repository, _auditEventRepo);
        var createVer = new CreateInstrumentVersionUseCase(_currentUser, _unitOfWork, _repository, _auditEventRepo);

        var inst = await createInst.ExecuteAsync(new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc", "Propósito"));
        var ver = await createVer.ExecuteAsync(new CreateInstrumentVersionCommand(inst.InstrumentId, 1, "Fuente", "Población", "Condiciones", "Licencia", "Limitaciones"));

        var getVerQuery = new GetAdministrativeInstrumentVersionByIdUseCase(_currentUser, _repository);
        var details = await getVerQuery.ExecuteAsync(inst.InstrumentId, ver.InstrumentVersionId);

        Assert.Equal(ver.InstrumentVersionId, details.InstrumentVersionId);
        Assert.Equal(inst.InstrumentId, details.InstrumentId);
        Assert.Equal(1, details.Version);
        Assert.Equal("DRAFT", details.Status);
        Assert.Equal("Fuente", details.SourceDescription);
        Assert.Equal("Población", details.Population);
        Assert.Equal("Condiciones", details.AdministrationConditions);
        Assert.Equal("Licencia", details.LicenseInformation);
        Assert.Equal("Limitaciones", details.Limitations);
    }

    [Fact]
    public async Task GetAdministrativeInstrumentVersionById_WithMismatchedInstrumentId_ShouldThrowNotFoundException()
    {
        var createInst = new CreateInstrumentUseCase(_currentUser, _unitOfWork, _repository, _auditEventRepo);
        var createVer = new CreateInstrumentVersionUseCase(_currentUser, _unitOfWork, _repository, _auditEventRepo);

        var inst1 = await createInst.ExecuteAsync(new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc", "Propósito"));
        var inst2 = await createInst.ExecuteAsync(new CreateInstrumentCommand("gad-7", "GAD-7", "Desc", "Propósito"));
        var ver = await createVer.ExecuteAsync(new CreateInstrumentVersionCommand(inst1.InstrumentId, 1, "Fuente", "Población", null, null, "Limitaciones"));

        var getVerQuery = new GetAdministrativeInstrumentVersionByIdUseCase(_currentUser, _repository);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            getVerQuery.ExecuteAsync(inst2.InstrumentId, ver.InstrumentVersionId));

        Assert.Contains("no existe para este Instrumento", ex.Message);
    }

    [Fact]
    public async Task Queries_WithoutAuthenticatedUser_ShouldThrowUnauthorizedException()
    {
        _currentUser.UserId = null;
        var listQuery = new ListAdministrativeInstrumentsUseCase(_currentUser, _repository);
        var getQuery = new GetAdministrativeInstrumentByIdUseCase(_currentUser, _repository);
        var getVerQuery = new GetAdministrativeInstrumentVersionByIdUseCase(_currentUser, _repository);

        await Assert.ThrowsAsync<UnauthorizedException>(() => listQuery.ExecuteAsync());
        await Assert.ThrowsAsync<UnauthorizedException>(() => getQuery.ExecuteAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedException>(() => getVerQuery.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
}
