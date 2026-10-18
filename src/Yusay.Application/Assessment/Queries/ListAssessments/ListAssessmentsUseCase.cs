using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Application.Assessment.Queries.ListAssessments;

public sealed class ListAssessmentsUseCase(
    ICurrentUser currentUser,
    IAssessmentAttemptRepository assessmentAttemptRepository,
    TimeProvider timeProvider) : IListAssessmentsUseCase
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAssessmentAttemptRepository _assessmentAttemptRepository = assessmentAttemptRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<ListAssessmentsResult> ExecuteAsync(
        ListAssessmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var ownerId = _currentUser.UserId;
        if (ownerId is null || ownerId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para consultar el historial de evaluaciones.");
        }

        var limit = query.Limit ?? DefaultLimit;
        if (limit is < 1 or > MaxLimit)
        {
            throw new ValidationException("El parámetro limit debe ser un entero entre 1 y 100.");
        }

        var cursor = AssessmentListCursor.Decode(query.Cursor);

        var rows = await _assessmentAttemptRepository.ListUserAttemptsAsync(
            ownerId.Value,
            limit + 1,
            cursor?.StartedAt,
            cursor?.AttemptId,
            cancellationToken);

        var currentTime = _timeProvider.GetUtcNow();

        var mappedRows = rows.Select(r =>
        {
            var effectiveStatus = (r.Status == "IN_PROGRESS" && currentTime >= r.ExpiresAt)
                ? "EXPIRED"
                : r.Status;

            AssessmentResultSummaryDto? result = null;
            if (effectiveStatus == "SUBMITTED" && r.Score.HasValue && !string.IsNullOrEmpty(r.InterpretationLabel))
            {
                result = new AssessmentResultSummaryDto(r.Score.Value, r.InterpretationLabel);
            }

            return new AssessmentListItemDto(
                r.AttemptId,
                r.InstrumentId,
                r.InstrumentCode,
                r.InstrumentName,
                r.InstrumentVersionId,
                r.Version,
                effectiveStatus,
                r.StartedAt,
                r.EndedAt,
                r.ExpiresAt,
                result);
        }).ToList();

        string? nextCursor = null;
        IReadOnlyList<AssessmentListItemDto> items;

        if (mappedRows.Count > limit)
        {
            items = mappedRows.Take(limit).ToList();
            var last = items[^1];
            nextCursor = AssessmentListCursor.Encode(last.StartedAt, last.AttemptId);
        }
        else
        {
            items = mappedRows;
        }

        return new ListAssessmentsResult(items, nextCursor);
    }
}
