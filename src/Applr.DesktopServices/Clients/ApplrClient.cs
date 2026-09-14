using System.Net.Http.Json;
using Applr.DesktopServices.Interfaces;
using Applr.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace Applr.DesktopServices.Clients;

public sealed class ApplrClient : IApplrClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApplrClient> _logger;

    public ApplrClient(
        HttpClient httpClient,
        ILogger<ApplrClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DbJobDto>> GetJobsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calling Applr API Trackr endpoint.");

        try
        {
            var response =
                await _httpClient.GetFromJsonAsync<List<DbJobDto>>(
                    "api/Trackr",
                    cancellationToken);

            if (response is null)
            {
                throw new InvalidOperationException(
                    "Applr API returned an empty response.");
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get scraped jobs from Applr API.");

            throw;
        }
    }
}
