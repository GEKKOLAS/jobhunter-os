using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JobHunter.Api.Middleware;

/// <summary>
/// Central exception → RFC 7807 mapping. Never leaks stack traces or upstream details to
/// clients; 5xx details go to the logs only (spec 02 §5).
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, errorCode) = exception switch
        {
            ValidationException => (StatusCodes.Status422UnprocessableEntity, "Validation failed.", "validation_failed"),
            NotFoundException notFound => (StatusCodes.Status404NotFound, notFound.Message, notFound.Code),
            UnauthorizedAppException unauthorized => (StatusCodes.Status401Unauthorized, unauthorized.Message, unauthorized.Code),
            ConflictException conflict => (StatusCodes.Status409Conflict, conflict.Message, conflict.Code),
            UpstreamUnavailableException upstream => (StatusCodes.Status502BadGateway, upstream.Message, upstream.Code),
            // Remaining use-case failures are input the server understood but can't act on.
            AppException app => (StatusCodes.Status422UnprocessableEntity, app.Message, app.Code),
            DomainException domain => (StatusCodes.Status400BadRequest, domain.Message, domain.Code),
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request cancelled.", "request_cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "internal_error"),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Extensions =
            {
                ["errorCode"] = errorCode,
                ["traceId"] = httpContext.TraceIdentifier,
            },
        };

        if (exception is ValidationException validation)
        {
            problemDetails.Extensions["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(g.Key),
                    g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }
}
