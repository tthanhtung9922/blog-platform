using Blog.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Blog.API.Middleware;

/// <summary>
/// Maps Application layer exceptions to RFC 9457 ProblemDetails HTTP responses.
/// Registered via AddExceptionHandler&lt;GlobalExceptionHandler&gt;() + UseExceptionHandler().
/// NEVER exposes stack traces, internal exception details, or connection strings.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        var (status, title, type, errors) = exception switch
        {
            ValidationException vex => (
                StatusCodes.Status422UnprocessableEntity,
                "Validation Error",
                "https://tools.ietf.org/html/rfc4918#section-11.2",
                vex.Errors),
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "Not Found",
                "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                (IDictionary<string, string[]>?)null),
            ForbiddenAccessException => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                (IDictionary<string, string[]>?)null),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                (IDictionary<string, string[]>?)null)
        };

        // Only log 5xx as errors — 4xx are expected client errors, log as warning
        if (status >= 500)
            logger.LogError(exception, "Unhandled exception");
        else
            logger.LogWarning("Handled exception {ExceptionType}: {Message}", exception.GetType().Name, exception.Message);

        context.Response.StatusCode = status;

        ProblemDetails problem = errors is not null
            ? new HttpValidationProblemDetails(errors) { Type = type, Title = title, Status = status }
            : new ProblemDetails { Type = type, Title = title, Status = status, Detail = status < 500 ? exception.Message : null };

        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
