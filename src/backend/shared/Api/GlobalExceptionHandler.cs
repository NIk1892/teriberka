using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api;

public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var innerException = exception.InnerException ?? exception;
        var errorMsg = GetErrorDetails(exception, out var statusCode);

        if (statusCode >= 500)
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogWarning(exception, "Request error {StatusCode} on {Method} {Path}", statusCode, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = statusCode;

        var activity = httpContext.Features.Get<IHttpActivityFeature>()?.Activity;

        activity?.AddException(innerException);

        var problemContext = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = innerException,
            ProblemDetails = new ProblemDetails
            {
               // Type = innerException.GetType().Name,
                Title = errorMsg,
              //  Detail = innerException.Message,
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}",
                Extensions = new Dictionary<string, object>
                {
                    { "requestId", httpContext.TraceIdentifier },
                    { "traceId", activity?.Id },
                }
            }
        };

        return await problemDetailsService.TryWriteAsync(problemContext);
    }

    private string GetErrorDetails(Exception exception, out int statusCode)
    {
        // Ошибку разбора тела запроса ASP.NET заворачивает в BadHttpRequestException, а внутрь
        // кладёт JsonException. Нужный код ответа знает только внешнее исключение, поэтому
        // смотрим его до разворачивания: иначе битый JSON от клиента отдавал бы 500 и падал
        // в лог как сбой сервера.
        if (exception is BadHttpRequestException badRequest)
        {
            statusCode = badRequest.StatusCode;
            return "Bad Request";
        }

        exception = exception.InnerException ?? exception;

        statusCode = exception switch
        {
            ValidationException => StatusCodes.Status422UnprocessableEntity,
            RequestValidationException => StatusCodes.Status400BadRequest,
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } => StatusCodes.Status409Conflict,
            PostgresException => StatusCodes.Status400BadRequest,
            ExcecuteCommandException ex =>  (int) ex.StatusCode,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            DbUpdateException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        var errorDetails = string.Empty;

        switch (statusCode)
        {
            case StatusCodes.Status422UnprocessableEntity:
                if (exception is ValidationException validationException)
                    errorDetails = string.Join(", ", validationException.Errors.Select(e => e.ErrorMessage));
                break;

            case StatusCodes.Status400BadRequest:
                errorDetails = exception.Message.Length > 0 ? exception.Message : "Bad Request";
                break;

            case StatusCodes.Status409Conflict:

                if (exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                    errorDetails = "The item already exists";
                else if (exception is DbUpdateConcurrencyException)
                    errorDetails = "The updated item does not exists or it has been changed by another user";
                else if (exception is ExcecuteCommandException excecuteCommandException)
                    errorDetails = excecuteCommandException.Message;

                break;

            default:
                if (statusCode >= 400 && statusCode < 500 && !string.IsNullOrEmpty(exception.Message))
                    errorDetails = exception.Message;
                else
                    errorDetails = "An error occurred while processing your request";
                break;
        }

        return errorDetails;
    }
}
