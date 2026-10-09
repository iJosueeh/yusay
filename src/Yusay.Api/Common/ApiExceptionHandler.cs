using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Common;

namespace Yusay.Api.Common;

internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger,
    IWebHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var mapping = Map(exception);
        if (mapping is null)
        {
            logger.LogError(
                exception,
                "Excepción no controlada al procesar {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);

            mapping = (
                StatusCodes.Status500InternalServerError,
                "Error interno del servidor",
                environment.IsDevelopment()
                    ? exception.Message
                    : "Se produjo un error inesperado. Vuelve a intentarlo o contacta con el responsable del servicio.");
        }
        else if (mapping.Value.StatusCode == StatusCodes.Status503ServiceUnavailable)
        {
            logger.LogError(
                exception,
                "Servicio no disponible al procesar {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        var (statusCode, title, detail) = mapping.Value;
        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
        return true;
    }

    private static (int StatusCode, string Title, string Detail)? Map(Exception exception) => exception switch
    {
        ValidationException failure => (
            StatusCodes.Status400BadRequest, "Solicitud no válida", failure.Message),
        DomainException failure => (
            StatusCodes.Status400BadRequest, "Solicitud no válida", failure.Message),
        InvalidTokenException failure => (
            StatusCodes.Status400BadRequest, "Token no válido", failure.Message),
        UnauthorizedException failure => (
            StatusCodes.Status401Unauthorized, "No autorizado", failure.Message),
        NotFoundException failure => (
            StatusCodes.Status404NotFound, "Recurso no encontrado", failure.Message),
        ConflictException failure => (
            StatusCodes.Status409Conflict, "Conflicto", failure.Message),
        ServiceUnavailableException failure => (
            StatusCodes.Status503ServiceUnavailable, "Servicio no disponible", failure.Message),
        _ => null
    };
}
