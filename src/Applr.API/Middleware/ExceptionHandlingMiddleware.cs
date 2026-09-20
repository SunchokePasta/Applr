using System.Text.Json;
using Applr.Services.Diagnostics;
using Applr.Services.DTOs;
using Refit;

namespace Applr.API.Middleware;

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

            HttpRequestException =>
                (StatusCodes.Status503ServiceUnavailable,
                    "The jobs service could not be reached. Check that Applr.RestApi is running."),

            TaskCanceledException or TimeoutException =>
                (StatusCodes.Status504GatewayTimeout,
                    "The jobs service took too long to respond."),

            System.Text.Json.JsonException =>
                (StatusCodes.Status502BadGateway,
                    "The jobs service sent back data Applr couldn't read."),

            InvalidOperationException =>
                (StatusCodes.Status500InternalServerError,
                    "Applr's API is misconfigured."),

            _ => (StatusCodes.Status500InternalServerError,
                    "Something went wrong.")
        };
}
