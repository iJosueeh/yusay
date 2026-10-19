using System.Data;
using Yusay.Application.Assessment.Common;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Repositories;

namespace Yusay.Application.Assessment.Queries.GetAssessmentGuidance;

public sealed class GetAssessmentGuidanceUseCase(
    ICurrentUser currentUser,
    IAssessmentAttemptRepository assessmentAttemptRepository,
    IResourceCatalogRepository resourceCatalogRepository,
    IUnitOfWork unitOfWork) : IGetAssessmentGuidanceUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAssessmentAttemptRepository _assessmentAttemptRepository = assessmentAttemptRepository;
    private readonly IResourceCatalogRepository _resourceCatalogRepository = resourceCatalogRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<AssessmentGuidanceDto> ExecuteAsync(
        GetAssessmentGuidanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para consultar la orientación de una evaluación.");
        }

        if (query.AttemptId == Guid.Empty)
        {
            throw new ValidationException("El identificador del intento de evaluación es obligatorio.");
        }

        return await _unitOfWork.ExecuteInReadOnlyTransactionAsync(async transaction =>
        {
            var attemptData = await _assessmentAttemptRepository.GetAttemptGuidanceDataAsync(
                query.AttemptId,
                userId.Value,
                transaction,
                cancellationToken);

            if (attemptData is null)
            {
                throw new NotFoundException(
                    "El intento de evaluación solicitado no existe, no pertenece al usuario autenticado o no posee un resultado oficial.");
            }

            var topics = await _resourceCatalogRepository.GetInterpretationTopicsAsync(
                attemptData.InterpretationId,
                transaction,
                cancellationToken);

            var resources = await _resourceCatalogRepository.ListPublishedAsync(
                topicId: null,
                dimensionId: null,
                interpretationId: attemptData.InterpretationId,
                type: null,
                limit: 10,
                cursorTitle: null,
                cursorResourceId: null,
                transaction: transaction,
                cancellationToken: cancellationToken);

            return new AssessmentGuidanceDto(
                attemptData.AttemptId,
                new AssessmentGuidanceInstrumentDto(
                    attemptData.InstrumentId,
                    attemptData.InstrumentCode,
                    attemptData.InstrumentName,
                    attemptData.InstrumentVersionId,
                    attemptData.Version),
                new AssessmentGuidanceResultDto(
                    attemptData.Score,
                    attemptData.CalculatedAt),
                new AssessmentGuidanceInterpretationDto(
                    attemptData.InterpretationId,
                    attemptData.InterpretationLabel,
                    attemptData.InterpretationDescription,
                    attemptData.InterpretationLimitations),
                topics,
                resources,
                AssessmentGuidanceConstants.NonClinicalDisclaimer);
        },
        IsolationLevel.RepeatableRead,
        cancellationToken);
    }
}
