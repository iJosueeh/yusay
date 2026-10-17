using System.Data;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;

namespace Yusay.Domain.UnitTests.Assessment.Fakes;

public sealed class FakeInstrumentAdministrationRepository : IInstrumentAdministrationRepository
{
    public List<Instrument> Instruments { get; } = new();
    public List<InstrumentVersion> Versions { get; } = new();

    public bool ThrowOnAddInstrument { get; set; }
    public bool ThrowOnAddVersion { get; set; }

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        var exists = Instruments.Any(i => string.Equals(i.Code.Value, code, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task<Instrument?> GetByIdAsync(Guid instrumentId, CancellationToken cancellationToken = default)
    {
        var inst = Instruments.FirstOrDefault(i => i.InstrumentId == instrumentId);
        return Task.FromResult(inst);
    }

    public Task<Instrument?> GetByIdForUpdateAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(instrumentId, cancellationToken);
    }

    public Task AddInstrumentAsync(
        Instrument instrument,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnAddInstrument)
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateCodeConflictMessage);
        }

        if (Instruments.Any(i => string.Equals(i.Code.Value, instrument.Code.Value, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateCodeConflictMessage);
        }

        Instruments.Add(instrument);
        return Task.CompletedTask;
    }

    public Task UpdateInstrumentAsync(
        Instrument instrument,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var idx = Instruments.FindIndex(i => i.InstrumentId == instrument.InstrumentId);
        if (idx >= 0)
        {
            Instruments[idx] = instrument;
        }

        return Task.CompletedTask;
    }

    public Task<bool> VersionNumberExistsAsync(
        Guid instrumentId,
        int version,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var exists = Versions.Any(v => v.InstrumentId == instrumentId && v.Version == version);
        return Task.FromResult(exists);
    }

    public Task<int> GetNextVersionNumberAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var existing = Versions.Where(v => v.InstrumentId == instrumentId).Select(v => v.Version).ToList();
        var next = existing.Count == 0 ? 1 : existing.Max() + 1;
        return Task.FromResult(next);
    }

    public Task AddVersionAsync(
        InstrumentVersion version,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnAddVersion)
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateVersionConflictMessage);
        }

        if (Versions.Any(v => v.InstrumentId == version.InstrumentId && v.Version == version.Version))
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateVersionConflictMessage);
        }

        Versions.Add(version);
        return Task.CompletedTask;
    }

    public Task<InstrumentVersion?> GetVersionByIdAsync(
        Guid instrumentVersionId,
        CancellationToken cancellationToken = default)
    {
        var ver = Versions.FirstOrDefault(v => v.InstrumentVersionId == instrumentVersionId);
        return Task.FromResult(ver);
    }

    public Task<IReadOnlyList<InstrumentSummaryDto>> ListAdministrativeInstrumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = Instruments
            .OrderBy(i => i.Name)
            .Select(i =>
            {
                var instVersions = Versions.Where(v => v.InstrumentId == i.InstrumentId).ToList();
                var count = instVersions.Count;
                var latest = count == 0 ? (int?)null : instVersions.Max(v => v.Version);
                return new InstrumentSummaryDto(
                    i.InstrumentId,
                    i.Code.Value,
                    i.Name,
                    i.Description,
                    i.Purpose,
                    count,
                    latest);
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<InstrumentSummaryDto>>(result);
    }

    public Task<InstrumentDetailsDto?> GetAdministrativeInstrumentDetailsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        var inst = Instruments.FirstOrDefault(i => i.InstrumentId == instrumentId);
        if (inst == null)
        {
            return Task.FromResult<InstrumentDetailsDto?>(null);
        }

        var instVersions = Versions
            .Where(v => v.InstrumentId == instrumentId)
            .OrderByDescending(v => v.Version)
            .Select(v => new InstrumentVersionSummaryDto(
                v.InstrumentVersionId,
                v.Version,
                v.Status.ToDbValue(),
                v.SourceDescription,
                v.Population,
                v.AdministrationConditions,
                v.LicenseInformation,
                v.Limitations))
            .ToList();

        return Task.FromResult<InstrumentDetailsDto?>(new InstrumentDetailsDto(
            inst.InstrumentId,
            inst.Code.Value,
            inst.Name,
            inst.Description,
            inst.Purpose,
            instVersions));
    }
}
