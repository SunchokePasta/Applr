using System.Net.Http.Json;
using System.Text.Json;
using Applr.DesktopServices.Exceptions;
using Applr.DesktopServices.Interfaces;
using Applr.Services.Diagnostics;
using Applr.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace Applr.DesktopServices.Clients;

/// <summary>
/// Posts to ApplrFiller's /apply and translates every failure into an
/// <see cref="ApplrApiException"/>, so MainWindow keeps having exactly one
/// exception type to handle however many services sit behind it.
///
/// ApplrFiller returns the same { message, reference, status } body shape on
/// failure that Applr.RestApi's middleware does, so the error read here is
/// the one written by whichever tier actually broke -- including the Python
/// one -- rather than a generic substitute.
/// </summary>
public sealed class ApplrFillerClient : IApplrFillerClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ApplrFillerClient> _logger;

    public ApplrFillerClient(
        HttpClient httpClient,
        ILogger<ApplrFillerClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApplyResult> ApplyAsync(
        uint jobId,
        string? url = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Asking ApplrFiller to fill {Target} for job {JobId}.",
            url ?? "the open page",
            jobId);

        HttpResponseMessage response;

        try
        {
            // Both fields are optional on the far side, so a null url still
            // means "whatever the browser has open" -- the body is sent
            // either way to keep one request shape.
            response = await _httpClient.PostAsJsonAsync(
                "apply",
                new ApplyRequest { Url = url, JobId = jobId },
                JsonOptions,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var reference = ErrorReference.New();

            _logger.LogError(
                exception,
                "[{Reference}] Could not reach ApplrFiller to fill job {JobId}.",
                reference,
                jobId);

            throw new ApplrApiException(
                exception is TaskCanceledException or TimeoutException
                    ? "The form filler took too long. It may still be typing -- check the browser."
                    : "The form filler isn't responding. Check that ApplrFiller.py is running, then try again.",
                reference,
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                var error = await ReadErrorAsync(response, cancellationToken);

                _logger.LogError(
                    "[{Reference}] ApplrFiller returned {StatusCode} for job {JobId}: {Message}",
                    error.Reference,
                    statusCode,
                    jobId,
                    error.Message);

                throw new ApplrApiException(error.Message, error.Reference);
            }

            try
            {
                var result = await response.Content.ReadFromJsonAsync<ApplyResult>(
                    JsonOptions,
                    cancellationToken) ?? new ApplyResult();

                _logger.LogInformation(
                    "ApplrFiller filled {FilledCount} fields for job {JobId}; " +
                    "{NotFilledCount} not filled, {UnmatchedCount} labels unrecognised.",
                    result.Filled.Count,
                    jobId,
                    result.NotFilled.Count,
                    result.UnmatchedLabels.Count);

                return result;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var reference = ErrorReference.New();

                _logger.LogError(
                    exception,
                    "[{Reference}] Could not read ApplrFiller's response for job {JobId}.",
                    reference,
                    jobId);

                throw new ApplrApiException(
                    "The form filler sent back data the app couldn't read.",
                    reference,
                    exception);
            }
        }
    }

    /// <summary>
    /// The body of POST /apply. A type rather than an anonymous object so the
    /// property names ApplrFiller binds against are stated once, next to the
    /// call that sends them.
    /// </summary>
    private sealed class ApplyRequest
    {
        public string? Url { get; set; }

        public uint JobId { get; set; }
    }

    private async Task<ApiErrorResponse> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(
                JsonOptions,
                cancellationToken);

            if (error is not null && !string.IsNullOrWhiteSpace(error.Message))
            {
                return error;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "ApplrFiller returned {StatusCode} with a body this app couldn't parse.",
                (int)response.StatusCode);
        }

        return new ApiErrorResponse
        {
            Message = "The form filler reported an error.",
            Reference = ErrorReference.New(),
            Status = (int)response.StatusCode
        };
    }
}
