using System.Net.Http.Json;
using JobFinder.DesktopServices.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobFinder.DesktopServices.Clients;

public sealed class JobFinderClient : IJobFinderClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JobFinderClient> _logger;

    public JobFinderClient(
        HttpClient httpClient,
        ILogger<JobFinderClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> GetJobsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calling JobFinder API connection endpoint.");

        var response = await _httpClient.GetFromJsonAsync<ConnectionResponse>(
            "api/ArbeitNow",
            cancellationToken);

        if (response is null)
        {
            throw new InvalidOperationException(
                "JobFinder API returned an empty response.");
        }

        _logger.LogInformation(
            "JobFinder API connection status: {Status}",
            response.Status);

        return response.Status;
    }

    private sealed record ConnectionResponse(string Status);
}
