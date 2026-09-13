using System.Net.Http.Json;
using JobFinder.DesktopServices.Interfaces;
using JobFinder.Services.DTOs;
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

    public async Task<IReadOnlyList<DbJobDto>> GetJobsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calling JobFinder API Trackr endpoint.");

        try
        {
            var response =
                await _httpClient.GetFromJsonAsync<List<DbJobDto>>(
                    "api/Trackr",
                    cancellationToken);

            if (response is null)
            {
                throw new InvalidOperationException(
                    "JobFinder API returned an empty response.");
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get scraped jobs from JobFinder API.");

            throw;
        }
    }
}
