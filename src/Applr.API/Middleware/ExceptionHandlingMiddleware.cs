using System.Text.Json;
using Applr.Services.Diagnostics;
using Applr.Services.DTOs;
using Refit;

namespace Applr.API.Middleware;

/// <summary>
/// One place where every unhandled exception in this API is caught,
/// logged with a reference id, and turned into an
/// <see cref="ApiErrorResponse"/> the desktop client can show the user.
/// Registered first in the pipeline (see Program.cs) so it wraps
/// everything downstream.
///
/// This API is a facade over Applr.RestApi and ArbeitNow, so most of
/// what lands here is somebody else's outage arriving as a Refit
/// ApiException or an HttpRequestException. The job of the mapping below
/// is to say *which* hop broke in terms a person can act on ("the jobs
/// service isn't running") rather than surfacing
/// "System.Net.Http.HttpRequestException: Connection refused" to a
/// React table.
///
/// Because this exists, TrackrService/ArbeitNowService no longer need
/// try/catch-log-rethrow around every call: an exception that escapes
/// them is logged exactly once, here, with the request path attached.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            // The desktop app hung up mid-request (window closed, user
            // hit Load again). Nothing is broken, so this is Information
            // -- otherwise every abandoned request looks like a fault.
            logger.LogInformation(
                "{Method} {Path} was cancelled by the caller.",
                context.Request.Method,
                context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
        catch (Exception exception)
        {
            await WriteErrorAsync(context, exception);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var (status, message) = Describe(exception);
        var reference = ErrorReference.New();

        logger.Log(
            status >= StatusCodes.Status500InternalServerError
                ? LogLevel.Error
                : LogLevel.Warning,
            exception,
            "[{Reference}] {Method} {Path} failed with {StatusCode}. TraceId {TraceId}.",
            reference,
            context.Request.Method,
            context.Request.Path,
            status,
            context.TraceIdentifier);

        // A Refit failure knows the upstream status and body. Worth its
        // own line: "502 because Applr.RestApi said 500" is a different
        // morning's work from "502 because it said 404".
        if (exception is ApiException apiException)
        {
            logger.LogError(
                "[{Reference}] Upstream {UpstreamMethod} {UpstreamUri} returned {UpstreamStatus}. Body: {UpstreamBody}",
                reference,
                apiException.HttpMethod,
                apiException.Uri,
                (int)apiException.StatusCode,
                apiException.Content ?? "(empty)");
        }

        if (context.Response.HasStarted)
        {
            logger.LogWarning(
                "[{Reference}] The response had already started; no error body was sent.",
                reference);

            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(
                new ApiErrorResponse
                {
                    Message = message,
                    Reference = reference,
                    Status = status
                },
                JsonOptions),
            context.RequestAborted);
    }

    /// <summary>
    /// Exception type -> (status code, message the user will read on the
    /// front end). Order matters here: ApiException derives from
    /// HttpRequestException, and TaskCanceledException derives from
    /// OperationCanceledException, so the specific arms have to come
    /// first. Keeping it as one switch expression is precisely so that
    /// ordering is visible in a single place instead of spread over a
    /// stack of catch blocks.
    /// </summary>
    private static (int Status, string Message) Describe(Exception exception) =>
        exception switch
        {
            // Upstream answered, but with a failure status.
            ApiException { StatusCode: System.Net.HttpStatusCode.NotFound } =>
                (StatusCodes.Status502BadGateway,
                    "The jobs service doesn't recognise that request. It may be running an older version."),

            ApiException =>
                (StatusCodes.Status502BadGateway,
                    "The jobs service returned an error. Nothing was changed."),

            // Upstream never answered: process not running, wrong port,
            // DNS, connection reset. By far the most likely failure when
            // Applr.RestApi simply hasn't been started.
            HttpRequestException =>
                (StatusCodes.Status503ServiceUnavailable,
                    "The jobs service could not be reached. Check that Applr.RestApi is running."),

            // HttpClient's own timeout surfaces as TaskCanceledException
            // with no cancellation requested; the caller-cancelled case
            // was already handled before we got here.
            TaskCanceledException or TimeoutException =>
                (StatusCodes.Status504GatewayTimeout,
                    "The jobs service took too long to respond."),

            // Upstream answered with something that isn't the JSON we
            // expect -- a schema drift between the two solutions.
            System.Text.Json.JsonException =>
                (StatusCodes.Status502BadGateway,
                    "The jobs service sent back data Applr couldn't read."),

            // Thrown by Program.cs for missing configuration.
            InvalidOperationException =>
                (StatusCodes.Status500InternalServerError,
                    "Applr's API is misconfigured."),

            _ => (StatusCodes.Status500InternalServerError,
                    "Something went wrong.")
        };
}
