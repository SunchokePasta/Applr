using System.Net.Http.Json;
using System.Text.Json;
using Applr.DesktopServices.Exceptions;
using Applr.DesktopServices.Interfaces;
using Applr.Services.Diagnostics;
using Applr.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace Applr.DesktopServices.Clients;

/// <summary>
/// Talks to Applr.API over HTTP. Every failure leaves here as an
/// <see cref="ApplrApiException"/> carrying a user-safe message and a
/// reference id, so nothing above this class has to inspect status
/// codes or exception types to decide what to show.
///
/// The previous version used GetFromJsonAsync/EnsureSuccessStatusCode,
/// which throw on a non-2xx *before* anything reads the body -- so the
/// explanation Applr.API had just written ("The jobs service could not
/// be reached. Check that Applr.RestApi is running.") was discarded and
/// the UI fell back to "Jobs could not be loaded. Please try again."
/// every time, whatever the real cause. Reading the body first is the
/// whole point of the rewrite.
/// </summary>
public sealed class ApplrClient : IApplrClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ApplrClient> _logger;

    public ApplrClient(
        HttpClient httpClient,
        ILogger<ApplrClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<IReadOnlyList<JobDto>> GetExistingJobsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Requesting existing jobs from Applr API.");

        return GetJobsAsync(
            "api/Trackr/jobs/existing",
            "load existing jobs",
            cancellationToken);
    }

    public Task<IReadOnlyList<JobDto>> GetNewJobsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Requesting new jobs from Applr API.");

        return GetJobsAsync(
            "api/Trackr/jobs/new",
            "load new jobs",
            cancellationToken);
    }

    public async Task PromoteNewJobsAsync(
        IReadOnlyList<uint> jobIds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Requesting Applr API to promote {JobCount} new jobs to Unreviewed.",
            jobIds.Count);

        using var response = await SendAsync(
            () => _httpClient.PostAsJsonAsync("api/Trackr/jobs/promote-new", jobIds, cancellationToken),
            "promote new jobs",
            cancellationToken);
    }

    private async Task<IReadOnlyList<JobDto>> GetJobsAsync(
        string path,
        string operation,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => _httpClient.GetAsync(path, cancellationToken),
            operation,
            cancellationToken);

        try
        {
            var jobs = await response.Content.ReadFromJsonAsync<List<JobDto>>(
                JsonOptions,
                cancellationToken);

            _logger.LogInformation(
                "Received {JobCount} jobs from {Path}.",
                jobs?.Count ?? 0,
                path);

            return jobs ?? [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A 200 whose body we can't parse: the two ends have drifted
            // apart (a DTO renamed on one side only). Distinct from the
            // failures SendAsync handles, and worth saying so.
            var reference = ErrorReference.New();

            _logger.LogError(
                exception,
                "[{Reference}] Could not read the response body while trying to {Operation}.",
                reference,
                operation);

            throw new ApplrApiException(
                "Applr's API sent back data the app couldn't read.",
                reference,
                exception);
        }
    }

    /// <summary>
    /// Runs the request and returns the response only when it succeeded.
    /// Two distinct failure shapes are handled here:
    ///
    /// 1. No response at all (API not running, wrong port, socket reset)
    ///    -- there is no body to read a reference out of, so one is
    ///    minted locally and written to the desktop log.
    /// 2. A response with a failure status -- Applr.API's middleware has
    ///    already put a message and a reference in the body, so those
    ///    are used as-is and the reference will appear in *both* logs.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        Func<Task<HttpResponseMessage>> send,
        string operation,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await send();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // We cancelled this ourselves. Not a failure to report.
            throw;
        }
        catch (Exception exception)
        {
            var reference = ErrorReference.New();

            _logger.LogError(
                exception,
                "[{Reference}] Could not reach Applr's API to {Operation}.",
                reference,
                operation);

            throw new ApplrApiException(
                exception is TaskCanceledException or TimeoutException
                    ? "Applr's API took too long to respond."
                    : "Applr's API isn't responding. Check that Applr.API is running, then try again.",
                reference,
                exception);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var statusCode = (int)response.StatusCode;
        var error = await ReadErrorAsync(response, cancellationToken);
        response.Dispose();

        _logger.LogError(
            "[{Reference}] Applr's API returned {StatusCode} when asked to {Operation}: {Message}",
            error.Reference,
            statusCode,
            operation,
            error.Message);

        throw new ApplrApiException(error.Message, error.Reference);
    }

    /// <summary>
    /// Reads Applr.API's <see cref="ApiErrorResponse"/> out of a failed
    /// response. Falls back to a generic message when the body is empty
    /// or isn't ours -- a proxy's HTML error page, say -- rather than
    /// throwing a second exception while handling the first.
    /// </summary>
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
                "Applr's API returned {StatusCode} with a body this app couldn't parse.",
                (int)response.StatusCode);
        }

        return new ApiErrorResponse
        {
            Message = "Applr's API reported an error.",
            Reference = ErrorReference.New(),
            Status = (int)response.StatusCode
        };
    }
}
