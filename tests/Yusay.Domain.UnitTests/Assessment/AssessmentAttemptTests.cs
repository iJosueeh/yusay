using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Common;

namespace Yusay.Domain.UnitTests.Assessment;

public sealed class AssessmentAttemptTests
{
    [Fact]
    public void Create_WithValidParameters_InitializesCorrectlyWith720HoursExpiry()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, now);

        Assert.NotEqual(Guid.Empty, attempt.AttemptId);
        Assert.Equal(userId, attempt.UserId);
        Assert.Equal(instrumentId, attempt.InstrumentId);
        Assert.Equal(versionId, attempt.InstrumentVersionId);
        Assert.Equal(AssessmentAttemptStatus.InProgress, attempt.Status);
        Assert.Equal(now, attempt.StartedAt);
        Assert.Equal(now.AddHours(AssessmentAttempt.AttemptValidityHours), attempt.ExpiresAt);
        Assert.Null(attempt.EndedAt);
        Assert.Equal(720, AssessmentAttempt.AttemptValidityHours);
    }

    [Fact]
    public void Create_WithEmptyGuids_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<DomainException>(() =>
            AssessmentAttempt.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), now));

        Assert.Throws<DomainException>(() =>
            AssessmentAttempt.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), now));

        Assert.Throws<DomainException>(() =>
            AssessmentAttempt.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, now));
    }

    [Fact]
    public void Expire_WhenInProgress_TransitionsToExpiredWithEndedAt()
    {
        var now = DateTimeOffset.UtcNow;
        var attempt = AssessmentAttempt.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now);
        var expiresAt = attempt.ExpiresAt;

        attempt.Expire(expiresAt);

        Assert.Equal(AssessmentAttemptStatus.Expired, attempt.Status);
        Assert.Equal(expiresAt, attempt.EndedAt);
    }

    [Fact]
    public void Expire_WhenNotInProgress_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var attempt = AssessmentAttempt.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now);
        attempt.Expire(attempt.ExpiresAt);

        Assert.Throws<DomainException>(() => attempt.Expire(attempt.ExpiresAt));
    }

    [Fact]
    public void Expire_WhenEndedAtBeforeStartedAt_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var attempt = AssessmentAttempt.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now);

        Assert.Throws<DomainException>(() => attempt.Expire(now.AddSeconds(-1)));
    }

    [Fact]
    public void Rehydrate_WithValidState_ReconstructsEntity()
    {
        var attemptId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow.AddDays(-35);
        var expiresAt = startedAt.AddHours(720);
        var endedAt = expiresAt;

        var attempt = AssessmentAttempt.Rehydrate(
            attemptId,
            userId,
            instrumentId,
            versionId,
            AssessmentAttemptStatus.Expired,
            startedAt,
            expiresAt,
            endedAt);

        Assert.Equal(attemptId, attempt.AttemptId);
        Assert.Equal(userId, attempt.UserId);
        Assert.Equal(instrumentId, attempt.InstrumentId);
        Assert.Equal(versionId, attempt.InstrumentVersionId);
        Assert.Equal(AssessmentAttemptStatus.Expired, attempt.Status);
        Assert.Equal(startedAt, attempt.StartedAt);
        Assert.Equal(expiresAt, attempt.ExpiresAt);
        Assert.Equal(endedAt, attempt.EndedAt);
    }

    [Fact]
    public void Rehydrate_WithInconsistentEndedAt_ThrowsDomainException()
    {
        var attemptId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var expiresAt = startedAt.AddHours(720);

        // InProgress but endedAt is not null
        Assert.Throws<DomainException>(() =>
            AssessmentAttempt.Rehydrate(
                attemptId, userId, instrumentId, versionId,
                AssessmentAttemptStatus.InProgress, startedAt, expiresAt, DateTimeOffset.UtcNow));

        // Expired but endedAt is null
        Assert.Throws<DomainException>(() =>
            AssessmentAttempt.Rehydrate(
                attemptId, userId, instrumentId, versionId,
                AssessmentAttemptStatus.Expired, startedAt, expiresAt, null));
    }
}

