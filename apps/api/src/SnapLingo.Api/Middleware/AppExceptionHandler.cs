using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SnapLingo.Application.Common;

namespace SnapLingo.Api.Middleware;

/// <summary>
/// Central mapping from Application exceptions to HTTP problem responses, so endpoints never
/// need their own try/catch. Anything unrecognized becomes a 500 with no internal detail
/// leaked to the client (spec section 13: never log secrets, and by extension never echo
/// internal exception detail back to callers either).
/// </summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (statusCode, title) = exception switch
        {
            AppValidationException => (StatusCodes.Status400BadRequest, exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message),
            LimitExceededException => (StatusCodes.Status403Forbidden, exception.Message),
            AuthenticationException => (StatusCodes.Status401Unauthorized, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title
        }, ct);

        return true;
    }
}
