using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Queries.ListResources;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.Content.Enums;

namespace Yusay.Application.Content.Queries.ListPublishedResources;

public sealed class ListPublishedResourcesUseCase(
    ICurrentUser currentUser,
    IResourceCatalogRepository resourceCatalogRepository) : IListPublishedResourcesUseCase
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IResourceCatalogRepository _resourceCatalogRepository = resourceCatalogRepository;

    public async Task<ListResourcesResult> ExecuteAsync(
        ListPublishedResourcesQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad autenticada para consultar recursos educativos.");
        }

        // Validar que se especifique como máximo un contexto: topicId, dimensionId o attemptId
        var contextCount = (query.TopicId.HasValue ? 1 : 0) +
                           (query.DimensionId.HasValue ? 1 : 0) +
                           (query.AttemptId.HasValue ? 1 : 0);

        if (contextCount > 1)
        {
            throw new ValidationException(
                "Solo se permite especificar un criterio de contexto por solicitud: topicId, dimensionId o attemptId.");
        }

        var limit = query.Limit ?? DefaultLimit;
        if (limit is < 1 or > MaxLimit)
        {
            throw new ValidationException("El parámetro limit debe ser un entero entre 1 y 100.");
        }

        string? type = null;
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            try
            {
                type = ResourceTypeExtensions.FromDbValue(query.Type).ToDbValue();
            }
            catch (ArgumentException)
            {
                throw new ValidationException($"El parámetro type '{query.Type}' no es válido (valores permitidos: ARTICLE, EXTERNAL_LINK).");
            }
        }

        Guid? interpretationId = null;
        if (query.AttemptId.HasValue)
        {
            interpretationId = await _resourceCatalogRepository.GetInterpretationIdForSubmittedAttemptAsync(
                query.AttemptId.Value,
                userId.Value,
                cancellationToken);

            if (interpretationId is null)
            {
                // Intentos ajenos, no SUBMITTED o inexistentes devuelven 404
                throw new NotFoundException("El intento especificado no existe o no cuenta con un resultado oficial disponible.");
            }
        }

        var cursorKey = ResourceListCursor.Decode(query.Cursor);

        var rows = await _resourceCatalogRepository.ListPublishedAsync(
            query.TopicId,
            query.DimensionId,
            interpretationId,
            type,
            limit + 1,
            cursorKey?.Title,
            cursorKey?.ResourceId,
            cancellationToken);

        string? nextCursor = null;
        IReadOnlyList<ResourceSummaryDto> items;

        if (rows.Count > limit)
        {
            items = rows.Take(limit).ToList();
            var last = items[^1];
            nextCursor = ResourceListCursor.Encode(last.Title, last.ResourceId);
        }
        else
        {
            items = rows;
        }

        return new ListResourcesResult(items, nextCursor);
    }
}
