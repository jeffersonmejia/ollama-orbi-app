using System.Data.Common;
using System.Net;
using Microsoft.AspNetCore.Diagnostics;

namespace OrbiApp.Presentation;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (httpContext.RequestAborted.IsCancellationRequested)
            return ValueTask.FromResult(true);

        var statusCode = GetStatusCode(exception);
        var failureType = FindFailureType(exception);

        if (statusCode == StatusCodes.Status503ServiceUnavailable)
        {
            logger.LogWarning(
                exception,
                "Request dependency failure. FailureType={FailureType} StatusCode={StatusCode} TraceId={TraceId}",
                failureType,
                statusCode,
                httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogError(
                exception,
                "Unhandled request failure. FailureType={FailureType} StatusCode={StatusCode} TraceId={TraceId}",
                failureType,
                statusCode,
                httpContext.TraceIdentifier);
        }

        // Let the configured exception path render the appropriate localized page or API response.
        return ValueTask.FromResult(false);
    }

    public static int GetStatusCode(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or TimeoutException or HttpRequestException)
                return StatusCodes.Status503ServiceUnavailable;

            if (current is OperationCanceledException)
                return StatusCodes.Status503ServiceUnavailable;
        }

        return StatusCodes.Status500InternalServerError;
    }

    private static string FindFailureType(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or TimeoutException or HttpRequestException or OperationCanceledException)
                return current.GetType().Name;
        }

        return exception.GetType().Name;
    }
}
