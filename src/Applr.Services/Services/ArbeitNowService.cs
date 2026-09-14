using Applr.Services.APIs;
using Applr.Services.DTOs;
using Applr.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Applr.Services.Services;

public sealed class ArbeitNowService : IArbeitNowService
{
    private readonly IArbeitNowApi _arbeitNowApi;
    private readonly ILogger<ArbeitNowService> _logger;

    public ArbeitNowService(
        IArbeitNowApi arbeitNowApi,
        ILogger<ArbeitNowService> logger)
    {
        _arbeitNowApi = arbeitNowApi;
        _logger = logger;
    }

    public async Task<ArbeitNowResponseDto> GetJobsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Requesting jobs from ArbeitNow API.");

        var response = await _arbeitNowApi.GetJobsAsync(
            cancellationToken);

        _logger.LogInformation(
            "Received {JobCount} jobs from ArbeitNow API.",
            response.Data.Count);

        return response;
    }
}
